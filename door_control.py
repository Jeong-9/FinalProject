import pigpio
import threading
from time import sleep

# ── GPIO 핀 번호 (BCM 기준) ──
SERVO1     = 18    # 내부문 1번 (물리핀 12)
SERVO2     = 13    # 내부문 2번 (물리핀 33)
LOCK_SERVO = 19    # 잠금장치   (물리핀 35)

# ── 서보 위치값 (마이크로초) ──
# pigpio 범위 : 500 ~ 2500  /  1500 = 중간(90도)
SERVO1_OPEN  = 2500    # 내부문1 열림
SERVO1_CLOSE = 700     # 내부문1 닫힘
SERVO2_OPEN  = 600     # 내부문2 열림
SERVO2_CLOSE = 1850    # 내부문2 닫힘
LOCK_HOME    = 2300    # 잠금 해제 위치
LOCK_LOCKED  = 1300    # 잠금 위치

# ── 이동 속도 설정 ──
# step  : 한 번에 이동할 펄스 크기  → 클수록 빠름
# delay : 스텝 사이 대기 시간(초)   → 작을수록 빠름
DOOR_OPEN_STEP   = 10
DOOR_OPEN_DELAY  = 0.02
DOOR_CLOSE_STEP  = 15
DOOR_CLOSE_DELAY = 0.012
LOCK_STEP        = 20
LOCK_DELAY       = 0.01


def limit_pulse(value):
    # 펄스값이 500~2500 범위를 벗어나면 서보/기구 파손 가능
    # 항상 이동 전에 범위 제한
    return max(400, min(3000, value))


class ServoController:
    # 잠금장치 + 내부문 통합 제어
    #
    # 시작 가정:
    #   내부문 → 닫혀있음
    #   잠금   → 열려있음 (LOCK_HOME)
    #
    # 상태 흐름:
    #   실험시작  : 잠금=잠김  내부문=닫힘  (experiment_start)
    #   로봇접근  : 잠금=잠김  내부문=열림  (open_for_robot)
    #   로봇완료  : 잠금=잠김  내부문=닫힘  (close_for_robot)
    #   잠금해제  : 잠금=열림  내부문=닫힘  (lock_open)
    #   잠금복귀  : 잠금=잠김  내부문=닫힘  (lock_close)
    #   실험종료  : 잠금=잠김  내부문=닫힘  (end_experiment)

    def __init__(self):
        self.pi = pigpio.pi()                    # pigpio 데몬 연결 (sudo pigpiod 먼저 실행)
        if not self.pi.connected:
            raise RuntimeError("pigpiod 연결 실패 — sudo pigpiod 먼저 실행!")

        self._mutex = threading.Lock()           # 동시에 여러 명령 들어와도 순서대로 처리

        # 시작 가정값 (experiment_start 호출로 실제 위치 맞춤)
        self.cur1     = SERVO1_CLOSE             # 내부문1 현재 위치
        self.cur2     = SERVO2_CLOSE             # 내부문2 현재 위치

        self.cur_lock = LOCK_HOME                # 잠금장치 현재 위치

    # ────────────────────────────────────────
    # 내부 이동 함수 (외부에서 직접 호출 금지)
    # ────────────────────────────────────────

    def _move_doors(self, target1, target2, step, delay):
        target1 = limit_pulse(target1)           # 목표 펄스 범위 제한
        target2 = limit_pulse(target2)

        diff1 = target1 - self.cur1              # 내부문1 이동해야 할 거리
        diff2 = target2 - self.cur2              # 내부문2 이동해야 할 거리
        max_d = max(abs(diff1), abs(diff2))      # 둘 중 더 긴 이동거리 기준으로 스텝 계산

        if max_d == 0:                           # 이미 목표 위치면 이동 생략
            print("[서보] 문: 이미 해당 위치 — 생략")
            return

        start1      = self.cur1
        start2      = self.cur2
        total_steps = max(1, int(max_d / step))  # 총 스텝 수 계산

        for n in range(1, total_steps + 1):
            ratio = n / total_steps              # 0~1 비율로 두 문이 동시에 시작·끝남
            self.pi.set_servo_pulsewidth(SERVO1, int(start1 + diff1 * ratio))
            self.pi.set_servo_pulsewidth(SERVO2, int(start2 + diff2 * ratio))
            sleep(delay)

        # 마지막 위치 정확히 확정
        self.pi.set_servo_pulsewidth(SERVO1, target1)
        self.pi.set_servo_pulsewidth(SERVO2, target2)
        self.cur1 = target1                      # 현재 위치 업데이트
        self.cur2 = target2

    def _move_lock(self, target):
        target = limit_pulse(target)             # 목표 펄스 범위 제한
        start  = self.cur_lock

        if target >= start:                      # 목표가 크면 정방향
            pulse_range = range(start, target + 1, LOCK_STEP)
        else:                                    # 목표가 작으면 역방향
            pulse_range = range(start, target - 1, -LOCK_STEP)

        for pulse in pulse_range:
            self.pi.set_servo_pulsewidth(LOCK_SERVO, pulse)
            sleep(LOCK_DELAY)

        # 마지막 위치 정확히 확정
        self.pi.set_servo_pulsewidth(LOCK_SERVO, target)
        self.cur_lock = target                   # 현재 위치 업데이트

    def is_locked(self):
        """잠금 상태 (True=잠김)"""
        return self.cur_lock == LOCK_LOCKED

    # ────────────────────────────────────────
    # 공개 명령
    # ────────────────────────────────────────

    def experiment_start(self):
        # [EXP_START] 실험 시작 — 내부문 닫기 + 잠금
        with self._mutex:
            print("[서보] ▶ 실험 시작 — 내부문 닫기")
            # 단계 이동 없이 바로 닫힘 위치로 (벌컥 방지)
            self.pi.set_servo_pulsewidth(SERVO1, SERVO1_CLOSE)
            self.pi.set_servo_pulsewidth(SERVO2, SERVO2_CLOSE)
            self.cur1 = SERVO1_CLOSE
            self.cur2 = SERVO2_CLOSE
            sleep(0.5)
            print("[서보] ▶ 잠금 적용")
            self._move_lock(LOCK_LOCKED)
            sleep(0.3)
            self.pi.set_servo_pulsewidth(LOCK_SERVO, 0)
            print("[서보] ▶ 잠금 완료")

    def open_for_robot(self):
        # [DOOR_OPEN] 로봇 접근 — 내부문 열기
        # 인터록: 잠금이 열려있으면 먼저 잠금 후 내부문 열기
        with self._mutex:
            print("[서보] ▶ 로봇 접근 — 내부문 열기")

            if self.cur_lock != LOCK_LOCKED:     # 잠금이 해제 상태면
                print("[서보] ⚠ 인터록 — 잠금 먼저")
                self._move_lock(LOCK_LOCKED)     # 먼저 잠금
                sleep(0.3)
                self.pi.set_servo_pulsewidth(LOCK_SERVO, 0)  # 토크 해제
                sleep(0.5)                       # 잠금 안정화 대기

            self._move_doors(SERVO1_OPEN, SERVO2_OPEN,
                             DOOR_OPEN_STEP, DOOR_OPEN_DELAY)
            print("[서보] ▶ 내부문 열림")

    def close_for_robot(self):
        # [DOOR_CLOSE] 로봇 완료 후 내부문 닫기
        with self._mutex:
            print("[서보] ▶ 로봇 완료 — 내부문 닫기")
            self._move_doors(SERVO1_CLOSE, SERVO2_CLOSE,
                             DOOR_CLOSE_STEP, DOOR_CLOSE_DELAY)
            print("[서보] ▶ 내부문 닫힘")

    def lock_open(self):
        # [LOCK_OPEN] 사용자 시약관 접근 — 잠금 해제
        with self._mutex:
            print("[서보] ▶ 잠금 해제")
            self._move_doors(SERVO1_CLOSE, SERVO2_CLOSE,
                             DOOR_CLOSE_STEP, DOOR_CLOSE_DELAY)
            self._move_lock(LOCK_HOME)
            print("[서보] ▶ 잠금 해제 완료")

    def lock_close(self):
        # [LOCK_CLOSE] 사용자 접근 완료 — 잠금
        with self._mutex:
            print("[서보] ▶ 잠금 복귀")
            self._move_lock(LOCK_LOCKED)
            sleep(0.3)
            self.pi.set_servo_pulsewidth(LOCK_SERVO, 0)
            print("[서보] ▶ 잠금 완료")

    def end_experiment(self):
        # [EXP_END] 실험 종료
        with self._mutex:
            print("[서보] ▶ 실험 종료 — 내부문 닫기")
            self._move_doors(SERVO1_CLOSE, SERVO2_CLOSE,
                             DOOR_CLOSE_STEP, DOOR_CLOSE_DELAY)
            print("[서보] ▶ 종료 완료")

    def stop(self):
        # 프로그램 종료 시 서보 토크 해제 + pigpio 연결 해제
        self.pi.set_servo_pulsewidth(SERVO1, 0)
        self.pi.set_servo_pulsewidth(SERVO2, 0)
        self.pi.set_servo_pulsewidth(LOCK_SERVO, 0)
        self.pi.stop()
        print("[서보] 연결 종료")

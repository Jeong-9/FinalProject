import socket
import threading
from doors.door_control import ServoController

PORT = 9003                      # WPF가 서보 명령 보내는 포트

servo = ServoController()        # 서보 컨트롤러 인스턴스 (전역 — 모든 명령이 여기 거쳐감)


def handle(conn):
    # 클라이언트(WPF)로부터 명령 수신 후 해당 서보 동작 실행
    try:
        data = conn.recv(1024).decode().strip()  # 명령 문자열 수신
        print(f"[ServoServer] 수신: {data}")

        # 서보 동작은 시간이 걸리는 blocking 작업
        # 별도 스레드로 실행해야 다음 명령 바로 받을 수 있음
        if data == "EXP_START":
            threading.Thread(target=servo.experiment_start, daemon=True).start()
        elif data == "DOOR_OPEN":
            threading.Thread(target=servo.open_for_robot,   daemon=True).start()
        elif data == "DOOR_CLOSE":
            threading.Thread(target=servo.close_for_robot,  daemon=True).start()
        elif data == "LOCK_OPEN":
            threading.Thread(target=servo.lock_open,        daemon=True).start()
        elif data == "LOCK_CLOSE":
            threading.Thread(target=servo.lock_close,       daemon=True).start()
        elif data == "EXP_END":
            threading.Thread(target=servo.end_experiment,   daemon=True).start()
        else:
            print(f"[ServoServer] 알 수 없는 명령: {data}")

    finally:
        conn.close()             # 처리 완료 후 연결 종료


def start():
    # 포트 9003에서 WPF 명령 대기
    # main.py에서 스레드로 호출됨
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)  # 재시작 시 포트 재사용
    s.bind(("0.0.0.0", PORT))
    s.listen(5)
    print(f"[ServoServer] 대기중 — {PORT}번 포트")

    while True:
        try:
            conn, addr = s.accept()                          # 연결 수락
            threading.Thread(target=handle, args=(conn,), daemon=True).start()  # 연결마다 스레드 처리
        except Exception as e:
            print(f"[ServoServer] 오류: {e}")


if __name__ == "__main__":
    start()

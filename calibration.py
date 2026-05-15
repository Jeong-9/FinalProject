"""
calibration.py
==============
사용자가 9개 마커를 순서대로 응시하면 동공 좌표를 자동 기록.
calibration_data.csv 저장 → Gaze CNN 학습 데이터.

실행 전 준비:
  1. measure_markers.py로 markers.csv 생성 완료
  2. OV9281 카메라 연결 (또는 임시로 일반 웹캠)
  3. 안경 마운트 착용
  4. 작업대 위 9개 마커 부착 상태

사용 방법:
  python calibration.py
  → 각 마커마다 안내문 표시 → 3초간 마커 응시 → 다음 마커
  → 9번 반복 후 calibration_data.csv 저장

주의:
  - 고개는 고정, 눈동자만 움직일 것
  - 마커 응시 중 깜빡이지 말 것
"""

import cv2
import csv
import os
import time
import numpy as np

# ─────────────────────────────────────────────────────────
# 설정
# ─────────────────────────────────────────────────────────
CAMERA_INDEX = 0          # OV9281이 보통 /dev/video0 → 0
DATA_DIR = "data"
MARKERS_FILE = os.path.join(DATA_DIR, "markers.csv")
OUTPUT_FILE = os.path.join(DATA_DIR, "calibration_data.csv")

# 동공 검출 파라미터
PUPIL_THRESHOLD = 50      # 픽셀값 50 이하 = 동공 (어두운 부분)
PUPIL_AREA_MIN = 300      # 동공 최소 면적 (px²)
PUPIL_AREA_MAX = 8000     # 동공 최대 면적 (px²)

DWELL_DURATION = 3.0      # 마커당 응시 시간 (초)
NUM_MARKERS = 9


def collect_pupil_coords(cap, duration=3.0, window_name="Calibration"):
    """duration초 동안 동공 좌표 수집 후 평균값 반환"""
    coords = []
    start = time.time()

    while time.time() - start < duration:
        ret, frame = cap.read()
        if not ret:
            continue

        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        _, binary = cv2.threshold(gray, PUPIL_THRESHOLD, 255, cv2.THRESH_BINARY_INV)
        contours, _ = cv2.findContours(
            binary, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE
        )

        # 동공 후보 중 가장 큰 윤곽선 1개만 사용
        best_cnt = None
        best_area = 0
        for cnt in contours:
            area = cv2.contourArea(cnt)
            if PUPIL_AREA_MIN < area < PUPIL_AREA_MAX and area > best_area:
                best_area = area
                best_cnt = cnt

        if best_cnt is not None:
            M = cv2.moments(best_cnt)
            if M["m00"] > 0:
                cx = M["m10"] / M["m00"]
                cy = M["m01"] / M["m00"]
                coords.append([cx, cy])
                cv2.circle(frame, (int(cx), int(cy)), 6, (0, 255, 100), -1)

        # 진행률 시각화
        progress = (time.time() - start) / duration
        bar_w = int(300 * progress)
        cv2.rectangle(frame, (10, 10), (310, 30), (50, 50, 50), -1)
        cv2.rectangle(frame, (10, 10), (10 + bar_w, 30), (0, 255, 0), -1)
        cv2.putText(
            frame,
            f"Look at marker  {progress * 100:.0f}%",
            (10, 60),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.7,
            (0, 255, 0),
            2,
        )

        cv2.imshow(window_name, frame)
        if cv2.waitKey(1) & 0xFF == 27:  # ESC 누르면 중단
            return None

    if len(coords) == 0:
        return None

    # 이상치 제거: 중앙값 ± 2σ 안의 값만 평균
    coords = np.array(coords)
    median = np.median(coords, axis=0)
    std = np.std(coords, axis=0)
    if std.sum() > 0:
        mask = np.all(np.abs(coords - median) < 2 * std, axis=1)
        coords_clean = coords[mask] if mask.sum() > 5 else coords
    else:
        coords_clean = coords

    return np.mean(coords_clean, axis=0)


def load_markers(csv_path):
    """markers.csv에서 마커 ID → 로봇 좌표 매핑 로드"""
    markers = {}
    with open(csv_path, "r", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            mid = int(row["marker_id"])
            markers[mid] = [
                float(row["X"]),
                float(row["Y"]),
                float(row["Z"]),
            ]
    return markers


def main():
    os.makedirs(DATA_DIR, exist_ok=True)

    # 마커 좌표 로드
    if not os.path.exists(MARKERS_FILE):
        print(f"✗ {MARKERS_FILE}이 없습니다. measure_markers.py를 먼저 실행하세요.")
        return

    markers = load_markers(MARKERS_FILE)
    print(f"✓ 마커 {len(markers)}개 로드 완료\n")

    # 카메라 열기
    cap = cv2.VideoCapture(CAMERA_INDEX)
    if not cap.isOpened():
        print(f"✗ 카메라 {CAMERA_INDEX}번 연결 실패")
        return
    print(f"✓ 카메라 {CAMERA_INDEX}번 연결\n")

    print("=== 시선 캘리브레이션 시작 ===")
    print("⚠ 주의: 고개는 고정하고 눈동자만 움직이세요.")
    print("⚠ ESC 누르면 중단됩니다.\n")

    results = []
    i = 1
    while i <= NUM_MARKERS:
        if i not in markers:
            print(f"✗ 마커 {i}번 좌표가 markers.csv에 없습니다.")
            i += 1
            continue

        robot_xy = markers[i]
        print(f"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
        print(f"[마커 {i}/{NUM_MARKERS}] 로봇 좌표 X={robot_xy[0]:.1f}  Y={robot_xy[1]:.1f}")
        input(f"  → 마커 {i}번을 바라볼 준비가 되면 Enter")
        print(f"  {DWELL_DURATION}초간 응시하세요...")

        pupil = collect_pupil_coords(cap, duration=DWELL_DURATION)

        if pupil is not None:
            results.append(
                [round(pupil[0], 2), round(pupil[1], 2), robot_xy[0], robot_xy[1]]
            )
            print(
                f"  ✓ 동공 ({pupil[0]:6.1f}, {pupil[1]:6.1f}) "
                f"→ 로봇 ({robot_xy[0]:.0f}, {robot_xy[1]:.0f})"
            )
            i += 1
        else:
            print(f"  ✗ 동공 검출 실패. 다시 시도합니다.")

    cap.release()
    cv2.destroyAllWindows()

    # 저장
    with open(OUTPUT_FILE, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["pupil_x", "pupil_y", "robot_X", "robot_Y"])
        writer.writerows(results)

    print(f"\n✓ {OUTPUT_FILE} 저장 완료 ({len(results)}개 데이터)")
    print("다음 단계: gaze_train.py 실행")


if __name__ == "__main__":
    main()

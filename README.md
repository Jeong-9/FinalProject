# 디팔렛타이저를 위한 영상처리 및 신경망 알고리즘 개발
<img width="4000" height="3000" alt="image" src="https://github.com/user-attachments/assets/bedd59c9-d359-4f77-9a46-dac79ff8ee01" />

<img width="499" height="301" alt="image" src="https://github.com/user-attachments/assets/fbc3699a-d8a2-4d69-a2f9-2241782b4f88" />


- **프로젝트명**: chemibot 비접촉 화학 실험 자동화 및 안전 관제 시스템
- **형태**: 팀 프로젝트
- **개발 인원**: 5명

## 프로젝트 개요 
유해 물질 노출 위험이 있는 화학 실험 환경에서, 연구자가 로봇팔을 원격 제어해 실험을 수행하는 비접촉 실험실 자동화 시스템

## 프로젝트 목적 
유해 화학물질 취급 과정에서 발생할 수 있는 안전사고를 예방하고, 손동작·얼굴 및 시선 추적 기반의 직관적인 인터페이스를 통해 연구자의 조작 편의성과 실험 효율을 향상시키는 것을 목표로 함

## 주요 기능
손동작 인식으로 로봇팔 제어 (시약 집기·붓기·섞기)

얼굴 방향 추적으로 화면 내 시험관대 선택

실시간 환경 모니터링 (가스·온도·습도)

실험실/보안실 모니터링(WPF), 관리자 웹(React)

비상정지 기능(스위치, 가스누출, 쓰러짐 감지)

# 개발 환경 및 사용 언어
- **개발 환경**: Visual Studio Code/Visual Studio
- **사용 언어**: C#, C++, Python
- **S/W 제작**: WPF, Flask, React
- **사용 라이브러리**: MediaPipe, OpenCV, RandomForest

- **통신**: TCP/IP/UDP , Socket.IO (WebSocket 기반), HTTP, UART Serial
- **임베디드 및 데이터베이스**: myCobot 280, RaspberryPi 4B (myCobot 내장), Arduino Uno, MySQL

# 하드웨어 구성도 
<img width="488" height="348" alt="image" src="https://github.com/user-attachments/assets/97cd771f-55a8-4de2-b152-17ee38d7ed72" /> <img width="197" height="340" alt="image" src="https://github.com/user-attachments/assets/ec3fa26b-8920-4135-afc9-03b08c514b66" />

# 시스템 구성
<img width="726" height="362" alt="image" src="https://github.com/user-attachments/assets/f8d4e6e8-d975-431d-b88a-663a6dd1230a" />

# 실험실 모니터링 프로그램 GUI 흐름도
<img width="750" height="388" alt="image" src="https://github.com/user-attachments/assets/00c22977-4ce1-4792-84bb-c08146ad5b17" />

# 비상 흐름도
<img width="1684" height="908" alt="image" src="https://github.com/user-attachments/assets/ba670c7f-44a8-4489-b43d-396630cba621" />

## 담당 구현 파일

| 파일 | 설명 |
|---|---|
| `LabSafetyManager/MainWindow.xaml` | 보안실 WPF 화면 UI 구성 |
| `LabSafetyManager/MainWindow.xaml.cs` | 영상 출력, 비상 처리, 상태 표시, 로그, 버튼 이벤트 처리 |
| `LabSafetyManager/FallTcpClient.cs` | TCP 9999 영상 수신 및 UDP 9998 비상 신호 처리 |
| `LabSafetyManager/IntercomService.cs` | 보안실 PC 마이크 음성을 UDP 10000으로 전송 |
| `LabSafetyManager/LogEntry.cs` | 이벤트 로그의 시간, 위험 단계, 메시지 관리 |
| `Servo/door_control.py` | 내부문 및 잠금장치 서보모터 제어 |
| `Servo/door_server.py` | TCP 9003 포트에서 서보 제어 명령 수신 |
| `lab_intercom.py` | UDP 10000 음성 수신 및 실험실 스피커 음성 출력 |

# 구현한 하드웨어 기능
## 1. 실험실 아크릴 박스 설계 및 제작 준비
<img width="331" height="562" alt="image" src="https://github.com/user-attachments/assets/42b859bb-bd26-4e4b-a13e-109713fc3b9c" />

- Solid Edge 설계 프로그램을 사용해서 실험실 아크릴 박스의 완성 예상도를 3D 모델링을 했습니다.

## 2. 2D 설계도면 작성 
<img width="516" height="423" alt="image" src="https://github.com/user-attachments/assets/48622e10-1ee5-4eac-8a8b-989be401f66f" />

- 완성된 3D 모델링을 참고해서 각 판의 설계도면을 DXF 파일 형식으로 변환했습니다. 각 판의 치수와 경첩 구멍 위치, 문 위치를 표시했습니다.

## 3. 아크릴 재단 및 조립
<img width="372" height="292" alt="image" src="https://github.com/user-attachments/assets/acaf6bbd-f339-4acd-b9cc-417a2b9a7173" />

- 완성된 도면을 기준으로 아크릴 판을 재단하고 실험 공간을 조립했습니다. 여기서 내부 유해 물질이 외부로 나가는 것을 방지하기 위해 외부 문과 내부 문으로 구성된 이중문 구조로 설계하고 조립했습니다.

## 4. 이중문 - 잠금장치, 내부 문 서보모터 제어

### 잠금장치
<img width="942" height="774" alt="image" src="https://github.com/user-attachments/assets/06d8ba9c-37d8-4d4d-af9c-bd42069f5cbd" />

### 내부 문 모터
<img width="733" height="449" alt="image" src="https://github.com/user-attachments/assets/6374e73d-43a0-4b56-9e4d-5adb95e58812" />

- 라즈베리파이에서  `pigpio`를 이용해 내부문 서보모터 2개와 잠금장치 서보모터를 제어했습니다.
- 내부문 1은 GPIO18, 내부문 2는 GPIO13, 잠금장치는 GPIO19에 연결했습니다.
- 각 모터의 열림·닫힘 위치를 직접 테스트하여 PWM 펄스폭 값을 설정했습니다.
- `door_server.py`는 TCP 9003 포트에서 명령을 수신하고, `door_control.py`의 서보 동작 함수와 연결합니다.
- 실험 시작, 내부문 열림·닫힘, 잠금 해제·복귀 명령을 구분하여 실행하도록 구현했습니다.
-  내부문 열림 명령이 들어올 때 잠금장치가 잠기지 않 상태라면 먼저 잠금장치를 잠근 뒤 내부문을 열도록 하여 실험 공간이 외부에 직접 노출되지 않도록 구성했습니다.
- 실험 종료 후에는 내부문을 닫은 뒤 사용자가 시약관을 회수할 수 있도록 잠금 해제 명령을 별도로 구성했습니다.

# 구현한 소프트웨어 기능

## 1. 보안실 화면
### 평상시 보안실 화면
<img width="1918" height="1078" alt="image" src="https://github.com/user-attachments/assets/01b93de7-2afe-47be-87ad-7e42a718e9cd" />

### 보안실 화면 버튼 기능

  <img width="322" height="233" alt="image" src="https://github.com/user-attachments/assets/c4df6dce-d025-40f9-9f5d-a1fa95979f48" />

- 보안실 화면은 C# WPF를 활용하여 제작했습니다.
- 중앙 카메라 영역은 실험실 측 PC에서 전송한 웹캠 영상을 TCP 9999 포트로 수신하여 실시간으로 출력합니다.
- 이벤트 로그는 TCP 연결, 비상 감지, 경고 해제, 음성 송신 등 주요 이벤트를 시간 순서대로 기록합니다. 정보·주의·위험 단계에 따라 색상을 구분하여 표시합니다.
- 시스템 상태에서 카메라 연결 상태와 비상 상태 여부를 확인할 수 있습니다.
- 버튼 기능에서 경고 해제 버튼을 누르면 보안실 WPF의 비상 화면과 사이렌을 종료하고, UDP 9998 포트로 비상 해제(clear) 신호를 전송하여 통제실 감지 상태를 초기화합니다
- 로그 저장 버튼은 이벤트 로그에 기록된 모든 상태와 감지 시각을 txt파일로 저장 할 수 있습니다.

## 2. 비상상황 발생 시 비상 신호 수신 및 음성 안내
### 비상 상황 발생 시 보안실 화면
  <img width="915" height="572" alt="image" src="https://github.com/user-attachments/assets/ec3ac6e6-86c9-4bb3-8725-30e8ed06492d" />

### 보안실 관리자의 마이크 음성 안내
  <img width="707" height="478" alt="image" src="https://github.com/user-attachments/assets/01ccce78-4627-4b85-87b1-970ed5545a1a" />

- 비상상황 발생 시 보안실 PC와 연결된 마이크를 통해 안내방송을 할 수 있습니다. 이 음성은 보안실에서 실험실 스피커로 전달됩니다.
- 비상 신호는 UDP 9998 포트로 수신하며, GAS_SENSOR, EME_BUTTON, FALL_DOWN 값을 통해 가스 감지, 비상 버튼, 쓰러짐 감지를 구분합니다.
- 실험실의 lab_intercom.py는 UDP 10000으로 수신한 보안실 음성을 실험실 스피커로 출력합니다.

# 시연 영상
https://youtu.be/Ujo4n_t4xfY

# 디팔렛타이저를 위한 영상처리와 신경망 알고리즘개발
<img width="4000" height="3000" alt="image" src="https://github.com/user-attachments/assets/bedd59c9-d359-4f77-9a46-dac79ff8ee01" />

<img width="499" height="301" alt="image" src="https://github.com/user-attachments/assets/fbc3699a-d8a2-4d69-a2f9-2241782b4f88" />


- **프로젝트명**: chemibot
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

# 구현한 하드웨어 기능
## 1. 실험실 아크릴 박스 설계 및 제작
<img width="331" height="562" alt="image" src="https://github.com/user-attachments/assets/42b859bb-bd26-4e4b-a13e-109713fc3b9c" />

- Solid Edge 설계 프로그램을 사용해서 실험실 아크릴 박스의 완성 예상도를 3D 모델링을 했습니다.

## 2. 2D 설계도면 작성 
<img width="516" height="423" alt="image" src="https://github.com/user-attachments/assets/48622e10-1ee5-4eac-8a8b-989be401f66f" />

- 완성된 3D 모델링을 참고해서 각 판의 설계도면을 DXF 파일 형식으로 변환했습니다. 각 판의 치수와 경첩 구멍 위치, 문 위치를 표시했습니다.

## 3. 아크릴 제단 및 조립
<img width="372" height="292" alt="image" src="https://github.com/user-attachments/assets/acaf6bbd-f339-4acd-b9cc-417a2b9a7173" />

- 완성된 도면을 기준으로 아크릴 판을 재단하고 실험 공간을 조립했습니다. 여기서 내부 유해 물질이 외부로 나가는 것을 방지하기 위해 외부 문과 내부 문으로 구성된 이중문 구조로 설계하고 조립했습니다.

## 4. 이중문 - 잠금장치, 내부 문 서보모터 제어

### 잠금장치
<img width="942" height="774" alt="image" src="https://github.com/user-attachments/assets/06d8ba9c-37d8-4d4d-af9c-bd42069f5cbd" />

### 내부 문 모터
<img width="733" height="449" alt="image" src="https://github.com/user-attachments/assets/6374e73d-43a0-4b56-9e4d-5adb95e58812" />

- 라즈베리파이에서 내부 문 서보 모터 2개와 잠금장치 서보 모터를 pigpio로 제어할 수 있습니다.
- 내부문 1은 GPIO18, 내부문 2는 GPIO13, 잠금장치는 GPIO19에 연결했습니다.
- 각 모터의 열림 닫힘 정도를 설정하기 위해 서보 위치값을 일일이 테스트해서 원하는 값을 확인하고 코드에 적용했습니다. 
- 제어구역 PC의 WPF화면에서 명령을 보내면 서버가 TCP로 수신해 해당 명령에 대응하는 서보 동작을 실행합니다. 실험 시작, 내부문 열림·닫힘, 잠금 해제·잠금 복귀 명령으로 설정했습니다.
- 실험이 시작되고, 내부문 열림 명령이 들어올 때 잠금 장치가 열려있으면 잠금 장치를 먼저 잠그고 내부 문을 열게 해서 격리된 실험 공간이 외부에 노출되는 것을 방지하도록 구현했습니다.
- 실험 종료도 마찬가지로 내부 문이 잠기면 잠금 장치를 열어서 다 쓴 시험관을 꺼낼 수 있도록 구현했습니다.   

# 구현한 소프트웨어 기능

## 1. 보안실 화면
### 평상시 보안실 화면
<img width="1918" height="1078" alt="image" src="https://github.com/user-attachments/assets/01b93de7-2afe-47be-87ad-7e42a718e9cd" />

### 보안실 화면 버튼 기능

  <img width="322" height="233" alt="image" src="https://github.com/user-attachments/assets/c4df6dce-d025-40f9-9f5d-a1fa95979f48" />

- 보안실 화면은 C# WPF을 활용해서 제작했습니다.
- 가운데에 있는 카메라 화면은 실험실에 연결된 CCTV 영상을 TCP 9999 포트를 이용해 실시간 모니터링이 가능합니다.
- 이벤트 로그는 비상 감지 시각과 현 상태를 순서대로 기록합니다. 현 상태에 따라 녹색, 황색, 적색으로 표시합니다.
- 시스템 상태에서 카메라 연결 상태와 비상 상태 여부를 확인 할 수 있습니다.
- 버튼 기능은 4가지로 되어있습니다. 그 중에 경보 해제 버튼은 보안실 관리자가 비상상황과 실험실 스피커에 나오는 대피 방송을 종료할 수 있습니다.

## 2. 비상 상황이 발생했을때 비상 신호 수신 및 은성 수신
### 비상 상황 발생 시 보안실 화면
  <img width="1917" height="1078" alt="image" src="https://github.com/user-attachments/assets/b15b5056-5252-4e07-864b-1665a09cd9cc" />

### 보안실 직원이 마이크로 안내 방송을 할 수 있습니다.
  <img width="707" height="478" alt="image" src="https://github.com/user-attachments/assets/01ccce78-4627-4b85-87b1-970ed5545a1a" />

- 비상상황 발생 시 보안실 PC와 연결된 마이크를 통해 안내방송을 할 수 있습니다. 이 음성은 보안실에서 실험실 스피커로 전달됩니다.

# 시연 영상


# 실제 인터넷 브라우저

현재 Windows x64에서 무료 오픈소스 **CEF 152.0.10 + CefSharp 152.0.100**을 사용한다. Unity 6000.6.0f1에서 실행하며, 기존 OS의 주소창·탭·사이드바 디자인 안에 실제 웹페이지를 표시한다. 유료 Unity 에셋이나 API 키는 필요하지 않다.

## 사용

브라우저 주소창에 `https://www.google.com/` 같은 주소, `example.com` 같은 도메인, 또는 검색어를 입력하고 Enter를 누른다. 검색어는 Google 검색으로 연결된다. 새 탭 화면의 검색창도 같은 방식이다.

- 실제 HTTP/HTTPS, JavaScript 페이지, 클릭·스크롤, 키보드와 한글 조합/확정 입력 전달
- 탭별 독립 페이지, 입력 내용·스크롤 유지, 최대 8개 탭
- 뒤로·앞으로·새로고침, 페이지 제목, 방문 기록, 북마크
- 창 크기에 맞는 웹 해상도, OS 전용 커서와 클릭 좌표 공유
- 모니터 조작 이탈·앱 포커스 상실·잠금 시 웹 키보드 포커스 해제
- `local://documents`, `local://archive`, `local://history`는 게임 내부 페이지로 유지

웹 파일 다운로드와 PC 파일 업로드는 이번 버전에서 지원하지 않는다. 기존 다운로드 아이콘은 게임의 가상 다운로드 폴더를 연다. 외부 사이트의 로그인, DRM 영상, 결제, 모든 IME/키보드 배치는 지원을 보장하거나 검증한 범위에 포함하지 않는다. 웹 엔진의 한글 조합/확정과 게임 입력 전달은 자동 검사했으며, 사용자 키보드의 실제 IME 조작감은 플레이 검수 대상이다.

## 새로 받은 저장소에서 준비

1. Windows에 .NET **10 SDK**를 설치한다. 프로젝트 개발용이다. 배포된 게임을 플레이할 때 SDK 설치는 필요하지 않다.
2. 프로젝트 루트에서 PowerShell로 실행한다.

```powershell
./Tools/BrowserHost/build.ps1
```

공식 NuGet 패키지를 고정된 잠금 파일로 복원한 뒤 `BrowserRuntime/Windows`에 자체 실행 가능한 호스트와 .NET 런타임, CEF 파일을 만든다. 이 실행 폴더는 약 **543 MB**이며 저장소에는 대형 실행 파일을 올리지 않는다. 원본 코드, 버전 잠금 파일과 빌드 스크립트를 관리한다. 브라우저 창을 닫거나 Play를 종료한 상태에서 엔진을 다시 빌드한다.

3. Unity에서 `Assets/Scenes/Desktop3D.unity`를 실행한다.
4. Windows x64 게임을 빌드하면 `WebBrowserBuild`가 실행 폴더와 라이선스를 `<게임명>_Data/BrowserRuntime`으로 복사한다. 엔진이 준비되지 않았다면 Windows 빌드를 중단하고 준비 명령을 안내한다.

## 구현

- `Tools/BrowserHost`: 별도 프로세스의 네이티브 Chromium 호스트. Unity에 CefSharp/.NET DLL을 직접 로드하지 않는다.
- `WebBrowserService`: 부모/자식 프로세스 전용 표준 입출력으로 명령과 상태를 전달한다. 통신용 네트워크 서버는 열지 않는다. 탭별 임의 이름 공유 메모리와 뮤텍스로 BGRA 프레임을 전달한다.
- `WebBrowserView`: 웹 프레임을 UI Toolkit 이미지에 올린다. 표시 사각형에 맞춰 모니터 패널 좌표를 웹 픽셀 좌표로 바꾼다. 한글 조합 문자열과 확정 문자를 분리하고, 커서·마우스·키보드·포커스를 연결한다.
- 창/탭 종료 시 페이지·공유 메모리·텍스처를 정리하고, 브라우저 창 종료 또는 Unity 종료 시 호스트도 종료한다. 호스트는 부모 프로세스가 사라지는 경우에도 종료한다.
- 캐시는 `Application.persistentDataPath/Desktop/WebCache`에 별도로 둔다. 실제 PC 브라우저 프로필은 가져오지 않는다. 가상 OS 파일을 웹사이트에 노출하는 스크립트 바인딩은 없다. HTTP/HTTPS 외 탐색, 네이티브 팝업 창, PC 파일 선택 및 다운로드 실행은 허용하지 않는다. 사용자 동작으로 연 웹 팝업 링크는 게임 내부 새 탭으로 보낸다.

외부 웹사이트는 내용·연결 상태가 바뀔 수 있으므로 퍼즐에 꼭 필요한 페이지는 게임 내부 자료로 만드는 구성을 권장한다.

## 검증

- `Tools/BrowserHost/smoke_test.py`: 실제 호스트에서 공개 HTTPS 페이지, 웹 프레임, 포인터 입력, 스크롤, 한글 조합/확정, 뒤로 이동, 해상도 변경을 검사한다. 로컬 검사 페이지는 테스트 중에만 127.0.0.1 임시 포트에서 제공한다.
- `WebBrowserVerification`: 실제 게임 OS에서 HTTPS 페이지 렌더, 입력 좌표, 한국어 문자열, 웹 버튼, 스크롤, 탭 상태 유지, 뒤로/앞으로, 창 크기/최대화, 포커스 해제와 종료를 검사한다. 기존 파일/메모/방/커서 검사와 함께 실행한다.
- `WebBrowserBuild.VerifyPlayerBuild`: 엔진을 포함한 Windows 개발 빌드를 만든다. 빌드된 게임을 `--verify-browser`로 실행하면 임시 저장 폴더를 사용해 실제 번들 엔진으로 HTTPS를 표시하고 결과를 출력한다. 일반 실행에서는 자동으로 사이트를 열거나 테스트 파일을 만들지 않는다.

[실행 화면](Images/unity-live-browser.png) · [검증 결과](web-browser-verification.txt) · [Windows 실행 파일 검증](browser-player-verification.txt)

## 오픈소스

[CEF](https://github.com/chromiumembedded/cef)와 [CefSharp](https://github.com/cefsharp/CefSharp)의 BSD 계열 라이선스를 사용한다. 실행 폴더의 `Licenses`에 CEF/CefSharp 라이선스와 .NET 라이선스·제3자 고지를 복사한다. 웹 엔진 업데이트 시 패키지 버전과 잠금 파일을 함께 갱신하고 동일한 검사를 다시 실행한다.

# VARCO 제작 기록

사용자 요청: BenQ ZOWIE XL2540X+, archon M3 600 MINI 블랙, ATK Phantom Verge Blue를 참고한 3D 게임 소품 3종.

## 참고 출처

- 모니터: https://zowie.benq.com/en-us/monitor/xl2540x-plus.html
- 키보드: https://www.preflow.co.kr/product/detail.html?product_no=10776
- 마우스: https://www.atk.store/products/atk-blazing-sky-phantom-hollow-carbon-fiber-composite-wireless-gaming-mouse

공식 사진은 제작 참고에만 사용했으며 원본 파일은 저장소에 포함하지 않는다. `References`에는 VARCO에서 생성한 제작용 이미지가 있다. 제조사 공식 3D 모델이나 정밀 복제품이 아닌 AI 생성 참고 모델이다. 키보드 각인과 배열, 마우스 타공 패턴 등의 세부 차이가 있다.

## 제작 설정

- VARCO Workflow: `4646dc1c-2408-4f66-b1d2-dabb3ace9750`
- EditImage: 제품별 1회, gpt-image-1.5-medium, 1:1.
- Generate3D: 제품별 1개, 삼각형, 2048 텍스처, PBR 켬.
- 목표 폴리곤: 모니터 16,000 / 키보드 24,000 / 마우스 20,000.
- 모니터는 조작 화면 가림을 피하기 위해 차광판과 별도 컨트롤러를 제외했다.
- 모니터의 인터랙티브 OS는 생성 모델에 구워 넣지 않고 Unity 화면 평면에 별도로 표시한다.
- 안내된 예상 생성 비용: 이미지 60 + 3D 600 = 660크레딧. 잔액은 MCP 응답에 제공되지 않음.

## 출력 노드

| 에셋 | 이미지 노드 | 3D 노드 |
|---|---|---|
| Monitor | 96434a89-e02a-4a6d-9798-9846693910da | 87946210-ce3c-44ce-9a28-2ad04c863fbb |
| Keyboard | 0cc087b1-1bed-42f5-affc-894aef22d46b | ec02044a-2dcb-4919-9021-3c3884ca9498 |
| Mouse | c91f3761-721c-4939-912a-e26ac502d3a5 | 98f3011b-7593-406b-959f-9d27557cac16 |

## 생성 상태 / 2026-10-06

제작용 이미지 3개는 성공했다. 텍스처·PBR 포함 첫 3D 생성은 세 개 모두 약 12분 후 실패로 반환됐다. 서버 응답에는 아래 task ID 외에 원인이 제공되지 않았다. 실제 크레딧 차감·환불 여부는 확인되지 않았다.

- Monitor: `84a8e551-437b-43e4-8e8c-d674c8d143d8`
- Keyboard: `65669d9c-2aac-4a96-adba-fc25b3c15e20`
- Mouse: `c1f305e2-8c96-41d4-b0c5-b84094325db5`

모니터만 별도 노드 `4921801f-ad70-41e2-8498-5ca3a45ef456`에서 텍스처·PBR 없이 형상 생성으로 재시도했다. 안내 비용은 100크레딧이다. 실패 노드와 성공 이미지는 보존했다.

형상만 생성하는 재시도도 실패했다: task `1fdd1a46-6115-4ff2-b113-9550bcf0d484`. 구체적인 원인은 제공되지 않았다. 추가 유료 재시도는 중단했으며 **완성된 3D 출력 파일은 없다**. 현재 Unity 씬은 직접 구성한 임시 장비를 사용한다. 이후 서비스에서 원인을 확인하거나 정상 생성이 가능해진 뒤 이어간다. 현재 상태는 `run-status.json`을 참고한다.

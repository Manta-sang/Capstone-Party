# 게임 안내와 최종 시상 화면

## 게임 안내
- `Assets/Scenes/GameBriefing.unity`의 `Game briefing controller > Guides`에서 5개 게임의 제목·규칙·조작법을 수정합니다.
- 배열 순서는 CrosswalkRun, SawBladeDodge, Qte, TileFlip, BlockFall입니다.
- 시작 버튼은 없습니다. 전원 로딩과 티켓 걷기 완료 후 서버 시간으로 5초를 세고 자동 이동합니다. Reading Seconds로 시간을 조정할 수 있습니다.
- 바코드 위의 Automatic departure countdown을 씬에서 배치할 수 있습니다. 텍스트는 실행 중 갱신됩니다.
- 게임 진입 후에는 티켓이 완전히 걷힌 뒤 1초 카운트다운과 조작 잠금이 이어집니다.

## 최종 시상
- 5번째 게임은 Ranking을 거치지 않고 FinalRanking으로 직접 이동합니다. 각 게임의 종료 처리가 `PartyGameRotation.ResultScene()`을 사용합니다.
- 실제 네트워크 플레이어를 점수 순으로 생성합니다. 우승자는 앞쪽에서 WASD 이동과 Space 점프가 가능하고, 비우승자는 뒤쪽에 쓰러진 자세로 배치합니다. 중앙 원형 받침 4개는 제거했습니다. 시상 중 밀치기는 비활성화합니다.
- 공동 1등도 모두 왕관을 받습니다. 왕관은 각 플레이어의 머리 위를 따라 회전·부유·점멸합니다.
- 오른쪽 중앙부터 4개 순위·닉네임·점수 행을 표시합니다. 1위는 더 큰 금색 글씨를 사용합니다.
- 전원 도착 후 15초 뒤 Character로 이동하며, 경기 점수·게임 순서·준비 상태를 초기화합니다.
- 고정 배경은 `Ruined airport · editable fixed scenery` 아래에 실제 씬 오브젝트로 저장됩니다. 기체 메시, 엔진 팬, 조종석·객실 창문, 날개·착륙장치, 유리 터미널, 탑승교, 관제탑, 수하물 카트, 파손 조각과 잔연을 편집할 수 있습니다.
- 생성 메시·재질·왕관은 `Assets/Prefabs/FinalRanking`, `Assets/Materials/FinalRanking`에 저장됩니다.

## 조명과 HUD
- 기존 5개 게임의 RenderSettings와 실제 Light 밝기를 보존합니다.
- 각 게임의 SceneLightingProfile은 원본 환경광·안개·스카이박스·태양 참조를 적용하고 DynamicGI 환경광을 갱신합니다. 시상식 설정을 다른 게임에 적용하지 않습니다.
- 타일플립 남은 시간은 검은색 64pt, 두 자리 초(60~00)로 표시됩니다.
- 밀치기 아이콘은 외부 폰트가 필요 없는 곡선 벡터 UI이며, 기존 원형 쿨타임과 소유자 연결을 유지합니다.

생성 도구를 다시 실행하면 해당 씬을 재생성하므로, 씬을 수동 편집한 뒤에는 `Build Finale and Briefing`을 실행하지 마세요.

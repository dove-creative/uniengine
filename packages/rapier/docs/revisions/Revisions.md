# Rapier 수정 기록

UniEngine이 사용하는 Rapier의 문제 상황과 해결방안을 요약한 문서이다. 구체적인 개조·검증 절차는 수정본별 계획에 기록한다.

## 0.36.1-r1 — 충돌 검사 이후 운동 갱신

2026-11-23에 0단계 수정본을 적용하고 네 구성의 Release·Debug 회귀 검증을 통과했다. `0.36.1-r1`은 Rapier 0.36.1에 대한 UniEngine 내부 패치 식별자이며 공식 배포 버전이 아니다. 제품 DLL 채택과 C# 구현은 후속 단계이다.

### 문제 상황

Rapier 0.36.1의 새 동적 강체에 첫 `Step` 전에 `DetectCollisions`를 호출하면 이후 Step에서도 높이와 속도가 바뀌지 않는다. 2D/3D × f32/f64 네 구성에서 재현했으며, Step만 실행한 대조 사례는 정상 낙하했다. 충돌 검사에서 소비한 변경 기록이 다음 물리 갱신에 전달되지 않아 활성 상태 등록이 누락되는 것으로 추정된다.

### 해결방안

무수정 `dt=0` 대안도 첫 Step 전 `검사 → 수면 → 검사`에서 수면 중 한 번 이동하여 채택하지 않았다. `PhysicsWorld::detect_collisions`가 복사한 설정의 시간 간격 0으로 실제 월드의 PhysicsPipeline을 실행하도록 바꾸고, 그 경로가 persistent island 초기화까지 완료하도록 두 파일을 수정했다. 별도 Rust 연결 DLL 구조와 수정하지 않은 Parry 0.31.1은 유지한다. 일반 CollisionPipeline 직접 호출까지 해결한 것은 아니다.

[개조·검증 계획](0.36.1-r1/Plan.md) · [원본·대안·수정본 검증 결과](../investigation/03.%20Results.md) · [업스트림 이슈 기록](Upstream%20Issue.md) · [업스트림 이슈 #1084](https://github.com/dimforge/rapier/issues/1084)

# Spatial System

Spatial System은 이미 검증된 외부 엔진을 UniEngine에 맞게 감싸, 공간 시스템의 기능을 일관된 방법으로 제공하는 Infrastructure의 공통 래퍼이다. 현재 구현 범위는 Collision System의 CollisionSpace 2D·3D와 Unity 연결이다. Physical System의 PhysicalSpace는 후속 구현으로 남긴다.

시스템의 정의·목적·제공 기능과 전체 흐름은 [Overview](01.%20Overview.md)에, 설정 변경·알림·수명의 공통 동작 규칙은 [Rules](02.%20Rules.md)에 정리한다. 지연 설정·관계 전환·오류 후 객체 제외는 소스에 반영하였다. Unity 실행 검증은 남아 있다.

## 목차

- 소개
- 구성과 책임
- 대표 흐름
- 구현 범위
- 상세 문서

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 외부 엔진의 독립된 월드·충돌 형상·공간 질의·접촉 및 Trigger 알림을 2D·3D 공통 API로 제공한다. |
| 목적 | 이미 검증된 외부 엔진을 UniEngine에 맞는 일관된 방법으로 사용하여, 소비자가 사용 중인 엔진을 몰라도 되게 한다. |
| 구현 | 공통 핸들에 C# 대리자를 주입하고 엔진별 래퍼가 객체 생성·값 변환·알림 연결을 맡는다. |

실제 공간 탐색과 충돌 판정은 외부 엔진이 수행한다. Spatial System은 독립적인 충돌 계산기나 전체 객체 쌍 검사를 구현하지 않는다. Unity 공간 인스턴스 하나는 전용 Scene과 로컬 물리 월드 하나를 소유한다.

## 구성과 책임

| 구성 | 현재 책임 |
| --- | --- |
| CollisionSpace2D·CollisionSpace3D | 월드 소유, 형상 생성, Update, 레이어 충돌 정책, 질의·알림 |
| ICollisionComponent·Collider 계열 | 공간 소속, 월드 자세, 로컬 형상, 활성·Trigger·레이어, 수명과 알림 |
| Unity 연결 | native 객체와 핸들 대응, 전용 Scene, 자동 중계와 내부 고정 강체 |
| PhysicalSpace | 미구현. 물리 운동과 공개 강체 기능은 후속 범위 |
| Rapier 연결 | 미구현 |

공통 네임스페이스는 BlackThunder.UniEngine.SpatialSystem, Unity 연결은 그 아래 Unity이다. 공통 계약에는 Unity 타입을 노출하지 않는다. 도메인 객체는 공통 핸들을 보유하여 사용하며 핸들 상속이나 인터페이스 구현을 요구받지 않는다.

공통 값에는 System.Numerics와 기존 Transform2D·Transform3D를 사용한다. 자세는 월드 좌표, 형상 매개변수는 로컬 기준이다. 공개 각도는 도, 시간은 초이다. 자세의 기준은 [Objects](../02-O.1-Objects/README.md), 단위와 실패 처리 기준은 [수학](../../../01.%20Concepts/05.%20수학.md)을 따른다. Core·Services에는 의존하지 않는다.

## 대표 흐름

1. 애플리케이션 조립부가 CollisionSpace3DUnity 같은 엔진별 공간을 생성한다.
2. 소비자에게 공통 CollisionSpace3D를 전달한다.
3. 소비자가 CreateBoxCollider 등의 공통 API로 형상을 만들고 자세·Trigger·레이어를 설정한다.
4. 호출자가 Update(deltaTime)으로 해당 월드를 한 번 진행한다. native 계산 후 접촉·Trigger 알림을 전달한다.
5. 질의로 같은 월드의 Collider 핸들과 공통 값을 얻는다. 질의가 시간을 진행하지는 않는다.
6. 개별 Collider 또는 월드를 Dispose한다. 월드 해제는 소속 핸들 정리 후 Disposed를 통지한다.

Unity의 알림 발생 조건을 충족하는 내부 고정 강체는 연결 코드가 관리한다. 공개 Rigidbody API나 강체 연결 속성은 제공하지 않는다. 물리 객체와 게임 화면 객체의 자세를 연결하는 작업은 애플리케이션이 맡는다.

## 구현 범위

[01-Common](../../../../src/01-Infrastructure/04-T-SpatialSystem/01-Common)과 [02.2-UnityWrappers](../../../../src/01-Infrastructure/04-T-SpatialSystem/02.2-UnityWrappers) 안에서 차원별 소스를 2D·3D로 나누고 공유 파일은 바로 아래에 둔다.

~~~text
04-T-SpatialSystem/
├─ 01-Common/
│  ├─ 2D/
│  ├─ 3D/
│  ├─ SpatialEngineException.cs
│  ├─ SpatialHandle.cs
│  └─ SpatialValues.cs
├─ 02.1-RapierWrappers/
└─ 02.2-UnityWrappers/
   ├─ 2D/
   ├─ 3D/
   └─ UnityBindingUtility.cs
~~~

기존 게임 씬의 객체를 자동 복제·이동·탐색하지 않는다. 공통 레이어 정책과 명시적인 Update는 월드마다 독립적이다. Unity의 엔진 고유 전역 설정은 [Engine Binding](04.%20Engine%20Binding.md)의 제한을 따른다.

PhysicalSpace·공개 Rigidbody·중력·힘·속도와 Collider의 강체 연결 API는 현재 소스에 두지 않는다. [Physical System](03.2.%20Physical%20System.md)에 후속 범위만 기록한다.

구현·검증 상태는 [테스트 결과](../../../../tests/01-Infrastructure/04-T-SpatialSystem/docs/03.%20Results.md)와 [이슈 기록](ISSUES.md)의 확인 범위를 따른다. 설계 기준의 문서화는 해당 동작의 구현이나 실행 검증 완료를 의미하지 않는다.

## 상세 문서

| 문서 | 설명 대상 |
| --- | --- |
| [Overview](01.%20Overview.md) | 시스템의 정체성·제공 기능·구조·대표 흐름 |
| [Rules](02.%20Rules.md) | 월드 조작의 경계·설정 적용·알림·수명·오류 처리의 규칙 |
| [Collision System](03.1.%20Collision%20System.md) | 공통 형상 생성·질의·알림·수명의 구조 |
| [Physical System](03.2.%20Physical%20System.md) | 후속 구현 범위 |
| [Engine Binding](04.%20Engine%20Binding.md) | 대리자 구성과 Unity 연결 |
| [Usage](Usage.md) | 공통 소비자와 Unity 조립 예시 |
| [ISSUES](ISSUES.md) | 미해결 항목과 범위 변경 상태 |
| [TODO](TODO.md) | 지금은 지원하지 않고 필요해지면 만들 기능 |

[Infrastructure](../README.md)

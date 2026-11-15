# Math

수치·벡터·회전의 비교와 보정을 제공하는 Infrastructure 수학 도구이다.

## 목차

- 소개
- 정의된 범위

## 소개

- 개요: 허용 오차를 사용한 값·방향·회전의 비교와 수치 보정을 제공한다.
- 목적: Unity 의존 없이 공통 수학 규칙을 재사용한다.
- 구현: BlackThunder.UniEngine.Mathematics의 정적 UniMath 클래스가 System.Numerics와 .NET 기본 수학 함수를 사용한다.

## 정의된 범위

Configuration과 같은 레벨이며 설정 초기화나 Core 객체에 의존하지 않는다. [VectorFunctions](../01-MT.2-VectorFunctions/README.md)도 같은 레벨의 독립 모듈이다.

[UniMath](UniMath.md)는 수치·벡터의 근사 비교, 스냅, 방향·회전 판정을 담당한다. 다음 레벨의 Transform2D·Transform3D는 UniMath를 사용해 위치와 회전의 근사 일치를 판정한다. [사용 예시](Usage.md)에서 호출 흐름을 확인한다.

단위·허용 오차·예외 처리의 공통 원칙은 [수학](../../../01.%20Concepts/05.%20수학.md)을 따른다.

값과 컬렉션의 표시 문자열은 [DataFormattingExtensions](../03-M.1-Utilities/DataFormattingExtensions.md), JSON 직렬화·역직렬화는 [Json](../03-M.1-Utilities/Json/README.md)이 담당한다.

[Infrastructure 구성](../README.md)

# LifecycleHub·EventHub 검증 결과

EventHub 경로 이전 당시의 통합 검증과 두 Hub 모듈 분리 이후의 검증을 함께 기록한다. 모듈 분리 이전의 EventHubTests 통과 수에는 LifecycleHub 사례가 포함되어 있다.

## 목차

- EventStream 키 행 구조의 EventHub 회귀 검증
- 실행 환경과 결과
- 확인한 변경 계약
- 기존 동작과 할당
- 문서 검증
- 산출물과 실행 명령
- 모듈 분리 검증
- 직접 생애주기 구독의 AdaptiveAction 전환
- LifecycleHub 구독 저장소 통합
- EventHub 등록 경로 통합
- 중복 등록 예외와 메시지 정리
- EventHub 구독 해제 알림 전환
- EventHub Connection 정리
- EventHub 비트 조건 경로
- EventHubBuilder 허브 의존성 분리
- EventHub 조건·콜백 결합형 등록

## EventStream 키 행 구조의 EventHub 회귀 검증

2026-11-11에 EventStream의 키 연산을 위치별 노드와 키 행으로 재편한 뒤 EventHub를 검증했다. Where와 Flags가 같은 위치에 섞이면 종류 노드의 생성 순서를 따르고, 각 노드 안에서는 행 생성 순서를 따른다. Where(a)→Flags(m)→Where(b) 등록은 a·b→flags 순서이다. 루트·직접 Updated 구독은 기존처럼 등록 순으로 먼저 호출한다.

EventHubFlagsTests의 기존 묶음 순서 사례를 WhereAndFlagsRowsStayGroupedInNodeCreationOrderAfterRootSubscriptions로 보강하여 위 순서를 확인했다. EventHub Tables·Plans도 같은 규칙과 새 테스트 이름으로 갱신했다. EventHubTests의 기존 루트·손자 순서와 리스너별 공유·해제·실패·할당 회귀는 그대로 통과했다.

| 실행 범위 | 통과 / 실행 | 실패 | 건너뜀 |
| --- | ---: | ---: | ---: |
| EventStream·EventHub 선택 실행 | 490 / 490 | 0 | 0 |
| 최종 전체 테스트 | 1,059 / 1,059 | 0 | 0 |
| 전체 실행 중 EventHub | 172 / 172 | 0 | 0 |

선택 실행 후 추가한 Debug 실패 회귀는 최종 전체 실행에 포함했다. Release 본체 빌드는 경고·오류 0건이며 전체 테스트 시간은 약 1분 23초이다. 최초 샌드박스 선택 실행은 testhost가 진행하지 않아 중단했으며 유효한 통과 수에서 제외했다.

EventHub 랜덤 연속 검증은 seed 0·1·2가 각각 독립 상태에서 깊이 100/100에 도달했다. 실제 실행시간은 각각 11.562 ms, 2.453 ms, 3.791 ms이며 모두 통과했다. 실제 단계 이력은 [전체 TRX](../../bin/TestResults/EventStreamKeyRows/stage1-full.trx)의 CombinedRegistrationsContinueWithRandomSeed StdOut에 있다. 전체 명령·EventStream 세부 결과는 [키 행 검증 결과](../02-E.3-EventStream/docs/03.%20Results.md)에 기록했다.

Unity Mono·IL2CPP 할당·스택 여유와 EventHub 자체의 벤치마크는 이번 검증 범위에 포함하지 않는다.

## 실행 환경과 결과

2026-11-10에 Windows·.NET SDK 10.0.403에서 EventHub의 EventStream 경로 이전을 검증했다. 본체는 C# 9·netstandard2.1, 테스트는 C# 9·net10.0의 Release 빌드이다.

| 실행 범위 | 결과 | 실패 | 건너뜀 |
| --- | ---: | ---: | ---: |
| 본체 솔루션 Release 빌드 | 성공 | 오류 0 | 해당 없음 |
| EventHubTests 범위 | 73 / 73 통과 | 0 | 0 |
| 전체 테스트 프로젝트 | 749 / 749 통과 | 0 | 0 |

본체 빌드에는 NuGet 취약성 정보 서버 https://api.nuget.org/v3/index.json 에 접근하지 못한 NU1900 경고가 2건 있었다. 전체 테스트의 복원·빌드에서도 같은 경고가 나타났다. 컴파일 오류는 없다.

첫 EventHub 실행은 샌드박스에서 testhost 연결이 90초 뒤 시간 초과되어 중단되었다. 같은 Release 산출물을 일반 실행 환경에서 --no-build --no-restore로 재실행하여 73개 모두 통과했다. 전체 테스트도 일반 실행 환경에서 복원·빌드를 포함해 실행했으며 749개 모두 통과했다. 중단된 시도는 통과 수에 포함하지 않는다.

## 확인한 변경 계약

- 리스너별 Branch·순서 그룹 없이 하나의 그래프를 공유하고, 서로 다른 리스너의 같은 Where 대리자는 갱신당 한 번 평가된다.
- 루트 구독은 리스너·직접 이벤트 구분 없이 등록 순서대로 섞이며, 자식 경로는 루트 구독 뒤 생성 순서로 방문한다. 기존 전파 중 추가·해제 테스트의 기대 순서를 이 계약에 맞췄다.
- typed·object Builder에서 Where(null)을 등록 시 거부하며 기존 갱신을 유지한다. EH-02가 해결되었다.
- Debug는 조건을 통과한 원래 값을 전달하고, 인자 없는 OnUpdated는 루트와 Where 뒤에서 같은 규칙으로 동작한다.
- 갱신 콜백 실패는 한 구독만 해제한다. 처리기는 한 번 보고받으며, 없거나 예외를 던지면 나머지 구독을 방문한 뒤 첫 예외를 전달한다. 같은 리스너의 다른 콜백·OnDisposed·다음 갱신은 유지된다.
- 직접 typed·object 이벤트의 실패 뒤 -=, 멀티캐스트의 마지막 연속 항목 제거, 같은 갱신 중 실패 항목을 제외한 제거가 안전하다. 실패한 콜백 대상은 허브가 살아 있어도 수거된다.
- 해제되거나 재연결 전의 Builder는 모든 등록을 거부하며, 허브 폐기 후에는 ObjectDisposedException이 우선한다.

멀티캐스트 갱신 등록은 호출 목록 항목마다 구독 하나로 나누었다. 이 선택으로 실패한 항목만 해제하면서 앞 항목의 Disconnect·Dispose가 뒤 항목을 즉시 건너뛰게 하는 계약을 함께 유지했다. EventStream·EventPath의 소스와 공개 API, LifecycleHub 동작, EH-03은 이번 변경에 포함하지 않았다.

## 기존 동작과 할당

Sleep의 네 진입점, 중첩 수면과 잘못된 Token 거부, 폐기 후 깨우기 생략을 확인했다. 생애주기 알림 순서와 IsDisposed 전환, Owner 전달, 생애주기 실패의 리스너 전체 해제, 갱신 중 폐기의 지연 정리도 유지되었다. 전체 실행에는 EventStream과 LifecycleHub의 회귀 검증이 포함된다.

| 측정 | 결과 |
| --- | ---: |
| 빈 구독·고정된 typed 구독의 첫 갱신 | 0 B |
| 루트·Where·인자 없는 멀티캐스트·직접 구독의 10,000회 갱신 | 0 B |
| EventHub·LifecycleHub의 등록된 콜백 폐기 순회 | 0 B |

등록 시점과 Debug 로그, object 어댑터의 박싱 비용은 이 측정에서 제외했다. 장시간 벤치마크와 Unity·외부 장비 검증은 실행하지 않았다.

## 문서 검증

설계·사용 문서를 갱신한 뒤와 테스트 문서 세 개를 추가한 뒤에 상대 링크를 검사했다. 마지막 검사에서 저장소가 직접 관리하는 Markdown 128개의 상대 링크 392개는 모두 대상이 존재했다. 외부 URL과 코드 예제 안의 링크는 검사 대상이 아니다. 변경한 코드·문서는 LF이며 git diff --check에서 공백 오류가 없었다.

## 산출물과 실행 명령

원본 결과는 tests/bin/TestResults/EventHubMigration/EventHub.trx와 All.trx이다. tests/bin은 .gitignore가 제외하는 빌드 산출물 경로이므로 원본 TRX는 버전 관리에 포함하지 않는다.

```powershell
dotnet build src/UniEngine.sln -c Release
dotnet test tests/UniEngine.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~EventHubTests --logger 'trx;LogFileName=EventHub.trx' --results-directory tests/bin/TestResults/EventHubMigration
dotnet test tests/UniEngine.Tests.csproj -c Release --logger 'trx;LogFileName=All.trx' --results-directory tests/bin/TestResults/EventHubMigration
```

[상태·동작 표](../03-E.2-EventHub/docs/01.%20Tables.md) · [테스트 계획](../03-E.2-EventHub/docs/02.%20Plans.md)

## 모듈 분리 검증

2026-11-10에 소스·문서·테스트를 03-E.1-LifecycleHub와 03-E.2-EventHub로 분리했다. Hub·Builder·Subject의 생애주기 계약을 별도 파일로 옮기고, 공유 내부 도구 HubCallbacks를 LifecycleHub 모듈에 배치했다. 네임스페이스와 공개 API, 두 Hub의 동작은 유지했다.

Windows·.NET SDK 10.0.403에서 기존 복원 산출물을 사용하여 본체와 테스트 프로젝트를 Release로 빌드했다. 본체는 netstandard2.1, 테스트는 net10.0이며 C# 9를 유지했다.

| 실행 범위 | 결과 | 실패 | 건너뜀 |
| --- | ---: | ---: | ---: |
| 본체·테스트 프로젝트 Release 빌드 | 성공, 경고 0·오류 0 | 0 | 해당 없음 |
| 전체 실행에 포함된 LifecycleHubTests | 5 / 5 통과 | 0 | 0 |
| 전체 실행에 포함된 EventHubTests | 68 / 68 통과 | 0 | 0 |
| 전체 테스트 프로젝트 | 749 / 749 통과 | 0 | 0 |

기존 통합 Hub 테스트 73개 중 LifecycleHub 사례 5개를 독립 클래스로 옮겼다. 전체 사례 수는 그대로이며, 두 클래스의 통과 수는 같은 전체 실행의 TRX에서 확인했다. 별도 범위 실행은 반복하지 않았다.

첫 전체 실행은 샌드박스의 testhost 연결이 90초 후 시간 초과되어 중단되었다. 동일한 Release 산출물을 일반 실행 환경에서 --no-build --no-restore로 다시 실행하여 749개 모두 통과했다. 테스트 실행 시간은 22초이며 중단된 시도는 통과 수에 포함하지 않는다.

이동 전후 본체의 타입 선언 14개를 비교하여 파일 경계와 using 배치 외에 구현이 바뀌지 않았음을 확인했다. LifecycleHub 소스에는 EventHub 타입 의존이 없으며, 기존 draw.io 파일은 내용이 동일한 상태로 폴더만 이동했다. 변경한 C# 파일은 LF이고 문서 상대 링크의 대상이 모두 존재한다. git diff --check에서 공백 오류는 없었다.

```powershell
dotnet build tests/UniEngine.Tests.csproj -c Release --no-restore --nologo
dotnet test tests/UniEngine.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=All.trx' --results-directory tests/bin/TestResults/HubSplit
```

원본 결과는 tests/bin/TestResults/HubSplit/All.trx이다. 두 Hub의 검증 기준은 [LifecycleHub 상태·동작 표](../03-E.1-LifecycleHub/docs/01.%20Tables.md)와 [EventHub 상태·동작 표](../03-E.2-EventHub/docs/01.%20Tables.md)에 각각 둔다.

## 직접 생애주기 구독의 AdaptiveAction 전환

2026-11-10에 LifecycleHub.OnDisposed와 EventHub.Disposing·Disposed의 저장소를 AdaptiveAction으로 바꾸고 HubCallbacks 구현과 상세 문서를 제거했다. 기존 AdaptiveAction과 ExceptionHandler 구현은 변경하지 않았다. Updated의 스트림 구독과 Builder로 등록한 수신자별 생애주기 콜백은 기존 처리 방식을 유지한다.

직접 생애주기 이벤트는 +=로 전달한 대리자 하나를 등록 항목 하나로 관리한다. -=는 첫 번째 일치 항목을 제거하며, 멀티캐스트는 전체 등록 묶음으로 해제한다. 묶음 내부에서 콜백이 실패하면 해당 등록 항목 전체를 제거하고 나머지 내부 호출을 생략한다. 예외 보고가 정상 반환하면 다음 등록을 계속 호출하고, 처리기가 없으면 원래 예외를 전파한다.

이 계약을 검증하는 LifecycleHub 사례 5개와 EventHub 사례 10개를 추가했다. 기존 무할당 폐기와 Disposing 중 해제 반영 사례도 같은 전체 실행에 포함했다.

| 실행 범위 | 결과 | 실패 | 건너뜀 |
| --- | ---: | ---: | ---: |
| 본체·테스트 프로젝트 Release 빌드 | 성공, 경고 0·오류 0 | 0 | 해당 없음 |
| 전체 실행에 포함된 LifecycleHubTests | 10 / 10 통과 | 0 | 0 |
| 전체 실행에 포함된 EventHubTests | 78 / 78 통과 | 0 | 0 |
| 전체 테스트 프로젝트 | 764 / 764 통과 | 0 | 0 |

Windows·.NET SDK 10.0.403에서 기존 복원 산출물을 사용했다. 이 세션에서 확인된 샌드박스의 testhost 연결 제한에 따라 테스트는 일반 실행 환경에서 동일한 Release 산출물로 실행했다. 전체 테스트 실행 시간은 9초이며 실패와 건너뜀은 없다. 별도 범위 테스트나 장시간 벤치마크는 반복 실행하지 않았다.

HubCallbacks를 참조하는 소스와 현재 설계 문서가 남아 있지 않음을 확인했다. 위 모듈 분리 절의 언급은 당시 검증의 이력이다. 상대 문서 링크 424개는 모두 유효하며 수정한 C# 파일은 LF이다.

```powershell
dotnet build tests/UniEngine.Tests.csproj -c Release --no-restore --nologo
dotnet test tests/UniEngine.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=All.trx' --results-directory tests/bin/TestResults/HubAdaptiveAction
```

원본 결과는 tests/bin/TestResults/HubAdaptiveAction/All.trx이다.

## LifecycleHub 구독 저장소 통합

2026-11-10에 LifecycleHub의 직접 OnDisposed 구독과 client 연결을 하나의 `Dictionary<object, Action>`으로 통합했다. client 연결은 client를 키로, 직접 구독은 대리자 자체를 키와 값으로 저장한다. 별도 _onDisposed 필드와 client 등록 시 GetInvocationList() 배열 생성을 제거하고, -=는 Disconnect와 같은 해제 경로를 사용한다. EventHub의 AdaptiveAction 사용은 유지했다.

직접 구독과 client 연결은 같은 키 공간을 사용하므로 경로에 관계없이 중복 키를 ArgumentException으로 거부한다. 직접·client 알림을 별도 단계로 실행하지 않으며 등록 간 통지 순서를 공개 계약으로 보장하지 않는다. 각 등록의 Action은 그대로 호출하고 멀티캐스트 내부 호출 순서는 유지한다. 폐기 상태를 먼저 확정하므로 순회 중 양쪽 경로의 해제·재폐기는 무시하고 새 등록은 거부한다.

직접 중복 구독의 기존 기대값을 중복 거부로 바꾸고, 두 경로의 키 충돌·교차 해제·null 처리·폐기 중 변경 차단을 추가 검증했다. 멀티캐스트 실패 검증은 직접 구독과 client 연결 모두에 적용하고 등록 간 호출 순서에는 의존하지 않도록 정리했다.

| 실행 범위 | 결과 | 실패 | 건너뜀 |
| --- | ---: | ---: | ---: |
| 본체·테스트 프로젝트 Release 빌드 | 성공, 경고 0·오류 0 | 0 | 해당 없음 |
| 전체 실행에 포함된 LifecycleHubTests | 18 / 18 통과 | 0 | 0 |
| 전체 실행에 포함된 EventHubTests | 78 / 78 통과 | 0 | 0 |
| 전체 테스트 프로젝트 | 772 / 772 통과 | 0 | 0 |

Windows·.NET SDK 10.0.403에서 기존 복원 산출물을 사용했다. 직접 구독의 공개 계약이 바뀌므로 전체 Release 테스트를 실행했으며, 이 세션에서 확인된 샌드박스 testhost 연결 제한에 따라 일반 실행 환경을 사용했다. 전체 실행 시간은 9초이다. 기존 할당 테스트에서 연결별·직접 콜백 네 개를 실행하는 폐기 구간의 관리 힙 할당은 0 B였다. 등록 비용의 별도 벤치마크는 수행하지 않았다.

상대 문서 링크 422개는 모두 유효하고 수정한 C# 파일은 LF이다. git diff --check에서 공백 오류가 없었다. EventHub·AdaptiveAction·ExceptionHandler와 기존 편집기 설정 변경은 보존했다.

```powershell
dotnet build tests/UniEngine.Tests.csproj -c Release --no-restore --nologo
dotnet test tests/UniEngine.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=All.trx' --results-directory tests/bin/TestResults/LifecycleUnified
```

원본 결과는 tests/bin/TestResults/LifecycleUnified/All.trx이다.

## EventHub 등록 경로 통합

2026-11-10에 EventHub의 직접 Updated·Disposing·Disposed 구독과 Connect 등록을 같은 Order 연결 모델로 통합했다. 직접 구독과 생성자 콜백은 원본 대리자를 키로 사용하며 ConnectionCount·HasListener에 포함된다. 별도 직접 구독 저장소를 제거하고 생애주기 등록은 AdaptiveCollection으로 통합했다. EventStream 자체와 기존 draw.io 파일은 변경하지 않았다.

같은 연결·이벤트·경로·콜백 형식의 동일한 원본 대리자는 InvalidOperationException으로 거부한다. typed/object Updated는 별도 등록이다. 멀티캐스트는 전체 묶음이 등록 하나이며, -=는 해당 이벤트만 해제하고 Disconnect는 연결 전체를 해제한다. 마지막 등록이 제거된 연결은 키까지 정리하며, 아직 등록하지 않은 빈 Connect 연결은 유지한다.

폐기는 모든 연결의 Disposing, IsDisposed 전환, 모든 연결의 Disposed 단계로 진행한다. 같은 연결·단계의 콜백은 오버로드와 관계없이 등록 순서이다. 갱신 실패는 등록 묶음을 제거하고, 생애주기 실패는 연결 전체를 제거한다. 순회 중 해제와 해제 직후 예외 보고, 같은 갱신 중 실패 콜백 재등록, 해제된 Builder의 대리자 키 참조 정리도 확인했다.

| 실행 범위 | 결과 | 실패 | 건너뜀 |
| --- | ---: | ---: | ---: |
| 본체·테스트 프로젝트 Release 빌드 | 성공, 경고 0·오류 0 | 0 | 해당 없음 |
| 전체 실행에 포함된 LifecycleHubTests | 18 / 18 통과 | 0 | 0 |
| 전체 실행에 포함된 EventHubTests | 105 / 105 통과 | 0 | 0 |
| 전체 테스트 프로젝트 | 799 / 799 통과 | 0 | 0 |

Windows·.NET SDK 10.0.403에서 기존 복원 산출물을 사용했다. 먼저 EventHub 99개 사례를 실행했고, 해제·예외·참조 정리 경계 사례 6개를 추가한 최종 상태에서 전체 799개를 실행했다. 전체 테스트 실행 시간은 8초이다. 이 세션에서 확인된 VSTest 샌드박스 연결 제한에 따라 테스트는 일반 실행 환경에서 실행했다.

기존 할당 테스트에서 고정된 typed 구독의 첫 갱신·10,000회 반복 갱신과 폐기 순회는 관리 힙 할당 0 B였다. 등록 시점의 비용, 값 형식의 object 박싱과 Debug 로그 비용은 이 측정에 포함하지 않는다. 별도 성능 벤치마크나 Unity 실행은 수행하지 않았다.

```powershell
dotnet build src/UniEngine.sln -c Release --no-restore
dotnet test tests/UniEngine.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=All.trx' --results-directory tests/bin/TestResults/EventHubUnified
```

최종 결과는 tests/bin/TestResults/EventHubUnified/All.trx이다. 관련 설계·API 문서와 테스트 상태·동작 표·계획을 갱신했다.

변경한 12개 파일의 기준본 대비 diff 공백 검사와 문서 상대 링크 41개 검사를 통과했다. 수정한 C# 파일은 LF이며, 기준본의 나머지 436개 파일은 해시가 동일하다. 기존 편집기 설정·ExceptionHandler·AdaptiveCollection·EventStream·LifecycleHub와 draw.io 변경을 보존했다.

## 중복 등록 예외와 메시지 정리

2026-11-10에 두 Hub의 중복 등록 예외 형식을 맞췄다. LifecycleHub는 직접 OnDisposed 구독과 client 연결이 같은 키를 다시 등록하면 Dictionary가 던지던 ArgumentException 대신 메시지가 있는 InvalidOperationException을 던진다. EventHub의 중복 등록 거부와 같은 형식이다. 기존 등록은 그대로 유지한다.

해제되었거나 재연결로 대체된 EventHub Builder의 InvalidOperationException 메시지에도 다른 EventHub 예외와 같은 '[EventHub: owner]' 접두어를 붙였다. LifecycleHub 종료자 경고의 형식 이름 오타(ILifecycledSubject)를 ILifecycleSubject로 고쳤다.

| 실행 범위 | 결과 | 실패 | 건너뜀 |
| --- | ---: | ---: | ---: |
| 본체·테스트 프로젝트 Release 빌드 | 성공, 경고 0·오류 0 | 0 | 해당 없음 |
| LifecycleHubTests | 18 / 18 통과 | 0 | 0 |
| EventHubTests | 105 / 105 통과 | 0 | 0 |
| 전체 테스트 프로젝트 | 799 / 799 통과 | 0 | 0 |

공개 예외 형식이 바뀌므로 전체 Release 테스트를 실행했다. LifecycleHubTests에서 중복 등록을 확인하던 세 사례의 기대 예외를 InvalidOperationException으로 바꿨으며 사례 수는 그대로이다.

## EventHub 구독 해제 알림 전환

2026-11-11에 EventHub의 매 갱신 후 구독 정리 순회를 Handle의 해제 알림으로 대체했다. 작업 시작 시 존재하던 EventHub.Order.cs 분리와 EventHub.cs 편집, 컨벤션 서브모듈의 변경은 보존했다.

### 구조와 동작

- Handle.Disposed의 접근 수준을 protected에서 protected internal로 넓혔다. 같은 어셈블리의 Order가 스트림 구독 핸들에 해제 알림을 등록한다.
- Order의 갱신 등록 목록은 `List<UpdateSubscription>`에서 `LinkedList<UpdateSubscription>`으로 바꿨다. 등록 순서와 -=의 첫 일치 규칙을 유지하면서 알림이 가리키는 노드를 O(1)에 제거한다. 제거한 노드의 값도 비워 알림 람다가 실패 콜백 대상을 붙잡지 않게 한다.
- 이미 폐기된 핸들은 알림을 추가하지 않고 같은 등록 정리 경로로 즉시 처리한다. 갱신 등록과 생애주기 콜백이 모두 사라진 연결은 Update 중이라도 즉시 제거한다. 아직 등록한 적 없는 빈 Connect 연결은 유지한다.
- 마지막 갱신 실패로 끊긴 Builder는 같은 Update 안에서도 InvalidOperationException으로 재등록을 거부한다. 직접 +=는 대리자 키로 새 연결을 만들며 다음 Update부터 호출한다. 생애주기 콜백이 남은 연결에서는 같은 경로의 같은 갱신 콜백을 다시 등록할 수 있다.
- PruneUpdateSubscriptions·PruneUpdates와 Update·AddUpdated·RemoveUpdated·RemoveLifecycleCallback의 관련 호출을 제거했다. -=는 핸들을 폐기하고 목록 제거는 알림 한 경로에 맡긴다.
- Order.Clear는 연결 상태를 먼저 끊으므로 핸들 폐기 알림이 순회 중인 목록을 바꾸지 않는다. 허브의 폐기 요청·폐기 진행·폐기 완료 상태에서는 알림을 무시하고 ReleaseReferences가 목록을 정리한다.
- Order는 Handle을 상속하지 않는다. 기존 IsConnected와 Clear만으로 연결 해제와 순회 보호를 표현할 수 있으며, 별도의 폐기 콜백 수명은 필요하지 않기 때문이다.

실패 감지·보고·갱신 구독 해제는 계속 EventStream이 담당한다. 허브에 갱신 콜백이나 조건을 감싸는 예외 처리를 추가하지 않았다. 생애주기 콜백 실패의 연결 전체 해제와 허브의 보고도 유지한다.

### 테스트와 빌드

NUnit 직접 사례 6개 메서드·11개 실행 조합을 추가했다.

| 추가 사례 | 실행 조합 | 확인한 계약 |
| --- | ---: | --- |
| AlreadyDisposedSubscriptionIsReleasedWhenRegistered | 2 | 이미 폐기된 핸들의 즉시 정리와 생애주기 등록 유무에 따른 연결 수명이다. |
| LastDirectUpdateRemovalDisconnectsImmediatelyDuringUpdate | 2 | typed·object 직접 -=로 마지막 등록을 제거한 직후 HasListener가 false이며 Builder가 무효화되는 계약이다. |
| LifecycleConnectedListenerCanReregisterAFailedCallbackOnTheSamePathDuringUpdate | 3 | typed·object·인자 없는 콜백 실패 뒤 같은 경로의 재등록과 다음 갱신 전달이다. |
| FailedWhereImmediatelyDisconnectsEmptyListenersAndKeepsLifecycleConnections | 1 | 공유 Where 실패 직후 같은 Update에서 빈 연결 제거·생애주기 연결 유지·남은 연결의 재등록을 확인한 것이다. |
| LifecycleConnectedOrderDoesNotRetainAFailedCallbackTarget | 1 | 연결이 생애주기 등록으로 남아 있어도 실패 콜백 대상은 수거되는 계약이다. |
| SubscriptionDisposalNotifiesEveryLifecycleConnection | 2 | 일반 폐기와 Update 중 폐기에서 모든 연결의 Disposing·Disposed 알림을 유지하는 계약이다. |

기존 FailedUpdateCanBeRegisteredAgainDuringTheSameUpdate(false)의 기대값만 바꿨다. 마지막 갱신 실패로 즉시 끊긴 Builder의 재등록은 InvalidOperationException이며 다음 갱신에서도 콜백을 실행하지 않는다. 직접 구독 조합은 같은 Update의 재등록을 허용하고 다음 갱신부터 호출한다. 나머지 기존 테스트 기대값은 변경하지 않았다.

| 실행 범위 | 결과 | 실패 | 건너뜀 |
| --- | ---: | ---: | ---: |
| 본체 솔루션 Release 빌드 | 성공, 경고 2·오류 0 | 0 | 해당 없음 |
| 전체 실행에 포함된 EventHubTests | 116 / 116 통과 | 0 | 0 |
| 전체 실행에 포함된 LifecycleHubTests | 18 / 18 통과 | 0 | 0 |
| 전체 테스트 프로젝트 | 899 / 899 통과 | 0 | 0 |

Windows·.NET SDK 10.0.403에서 실행했다. 본체 빌드의 두 경고는 api.nuget.org의 취약성 정보 조회 실패인 NU1900이다. 전체 테스트의 복원·빌드에서도 같은 경고가 나타났다. 첫 전체 실행 시도는 새 object Builder 테스트에서 int 전용 검증 도우미를 호출한 CS1503으로 컴파일 단계에서 중단되었다. 해당 단언의 형식을 수정한 최종 실행은 1분 30초에 899개 모두 통과했다. 테스트는 testhost와 자식 프로세스 실행이 가능한 일반 실행 환경에서 수행했다.

기존 첫·반복 Update와 폐기 순회의 관리 힙 할당 0 B 검증, FailedSubscriptionsDoNotRetainTheirCallbackTargets의 두 조합도 모두 통과했다. 등록 시점의 람다·연결 리스트 노드 할당, object 경로의 값 형식 박싱과 Debug 출력 비용은 무할당 보장에 포함하지 않는다.

```powershell
dotnet build src/UniEngine.sln -c Release
dotnet test tests/UniEngine.Tests.csproj -c Release --logger 'trx;LogFileName=All.trx' --results-directory tests/bin/TestResults/EventHubDisposalNotifications
```

최종 결과는 tests/bin/TestResults/EventHubDisposalNotifications/All.trx이다. 실패한 컴파일 시도는 통과 수에 포함하지 않는다.

### 성능 확인

변경 전은 커밋 2a66c2d06ed03d44e5566a06c1b84d74821aa497의 소스이며, 변경 후는 이 작업 트리의 소스를 임시 폴더로 복제한 것이다. 같은 C# 측정 프로그램을 각각의 Release DLL에 연결했다. 두 실행의 측정 프로그램 DLL은 SHA256가 같으며, 프로그램과 비교용 소스는 저장소 밖 임시 폴더에 두었다.

측정 환경은 Intel Core i7-14650HX, Windows, .NET 10.0.14 x64, SDK 10.0.403이다. 두 프로세스에서 ProcessorAffinity=0x3과 PriorityClass=High가 실제 적용되었음을 확인했다. 0x3은 논리 CPU 0·1을 선택하는 마스크이며 물리 코어 토폴로지는 별도로 확인하지 않았다. TieredCompilation과 TieredPGO는 두 실행 모두 활성화했다.

각 리스너는 같은 조건 대리자의 Where 뒤에 구독한다. 조건이 항상 거짓인 경우와 항상 참이어서 모든 리스너가 받는 경우를 각각 측정했다. 시나리오마다 1초 이상 예열하고 배치당 반복 횟수를 조정한 뒤 9라운드의 중앙값을 비교했다. 표의 단위는 ns/Update이며 괄호는 최솟값이다. Update를 호출하는 반복문 비용을 포함하고 등록·폐기·출력 비용은 제외했다. 변경 전과 후 프로세스는 전체 테스트 종료 뒤 순서대로 실행했다.

| 조건 | 리스너 수 | 변경 전 중앙값 (최솟값) | 변경 후 중앙값 (최솟값) |
| --- | ---: | ---: | ---: |
| 항상 거짓 | 10 | 282.992 (278.669) | 7.434 (7.293) |
| 항상 거짓 | 100 | 2,321.800 (2,277.805) | 7.599 (7.586) |
| 항상 거짓 | 1000 | 22,875.476 (22,712.451) | 7.367 (7.344) |
| 모두 전달 | 10 | 309.569 (307.036) | 50.072 (49.661) |
| 모두 전달 | 100 | 2,860.324 (2,834.714) | 511.282 (509.238) |
| 모두 전달 | 1000 | 27,673.706 (27,569.604) | 5,278.607 (5,249.109) |

항상 거짓인 경우 변경 후 중앙값은 7.367~7.599ns로 리스너 수와 거의 무관하다. 모두 전달하는 경우에는 실제 콜백 호출 수에 비례하는 비용이 남지만 전체 연결 정리 순회 비용은 사라졌다. 모든 시나리오에서 수신 횟수 검산을 통과했으며 9라운드 측정 구간의 관리 힙 할당 합계는 각각 0 B였다. 이 결과는 해당 .NET 환경의 간단한 측정이며 Unity Mono·IL2CPP의 실행 결과는 아니다.

원시 측정 결과는 tests/bin/TestResults/EventHubDisposalNotifications/BenchmarkBefore.txt와 BenchmarkAfter.txt이다. 측정 코드는 저장소에 추가하지 않았다.

### 문서와 범위 확인

[EventHub](../../../docs/02.%20UniEngine/02-Core/03-E.2-EventHub/EventHub.md), [Handle](../../../docs/02.%20UniEngine/02-Core/02-E.1-HandleToken/Handle.md), [EventHub 상태·동작 표](../03-E.2-EventHub/docs/01.%20Tables.md), [EventHub 테스트 계획](../03-E.2-EventHub/docs/02.%20Plans.md)과 이 결과 문서를 갱신했다. 이전 결과 절의 내용은 유지하고 목차에 이번 절을 추가했다.

저장소가 직접 관리하는 Markdown 136개의 기존 상대 링크 450개는 모두 유효했다. 이번 결과 절에서 추가한 상대 링크 4개도 대상이 존재한다. 변경 코드·문서는 LF이며 공백 검사도 통과했다.

결정 사항에서 벗어나거나 추가 결정을 요청해야 하는 문제는 없었다. EventStream 소스·LifecycleHub 동작·MVP 저장소·draw.io 파일은 수정하지 않았고, 커밋과 푸시는 하지 않았다.

## EventHub Connection 정리

2026-11-11에 연결 하나의 등록을 맡는 내부 형식 Order의 이름을 Connection으로 바꾸고 책임을 Connection으로 모았다. 공개 API와 동작은 바꾸지 않았다.

- 같은 연결의 중복 검사, 갱신 구독 생성과 -= 해제, 생애주기 콜백의 추가·제거, 폐기 알림 호출, 빈 연결 정리를 Connection이 수행한다. 허브는 키와 Connection의 대응, 공유 EventStream, 갱신·수면·폐기 진행만 맡는다.
- 갱신 등록 기록 형식 UpdateSubscription을 Subscription으로 바꾸고 Connection 안의 private 형식으로 두었다. Connection의 필드와 등록 목록도 모두 private이다. 허브와 Builder는 생성자와 등록·해제·알림 메서드만 사용한다.
- 연결을 끝내는 일은 Connection.Dispose 하나로 모았다. Dispose는 여러 번 불러도 한 번만 동작하며, 구독 핸들과 생애주기 콜백을 놓고 허브 사전에서 자기 항목을 지운다. Disconnect, 등록이 모두 사라진 경우, 생애주기 콜백 실패, 허브 폐기가 모두 이 경로를 쓴다. 허브의 RemoveConnection은 사전 항목만 지운다.
- Builder는 루트 경로를 만들 때 쓰는 허브를 직접 받고, 사용 가능 여부는 Connection에 확인한다. 테스트용 재정의에만 쓰이던 GetBuilder는 private으로 바꿨다.
- 이미 폐기된 핸들을 내부 형식으로 직접 등록하던 AlreadyDisposedSubscriptionIsReleasedWhenRegistered(2개 사례)와 그 보조 클래스를 지웠다. 공개 API로는 이 상태를 만들 수 없으며, 방어 분기는 코드에 남겼다. EventHub 표·계획의 F9 행도 함께 지웠다.

| 실행 범위 | 결과 | 실패 | 건너뜀 |
| --- | ---: | ---: | ---: |
| 본체·테스트 프로젝트 Release 빌드 | 성공, 경고 0·오류 0 | 0 | 해당 없음 |
| EventHubTests | 114 / 114 통과 | 0 | 0 |
| LifecycleHubTests | 18 / 18 통과 | 0 | 0 |
| 전체 테스트 프로젝트 | 897 / 897 통과 | 0 | 0 |

빌드 경고는 패키지 취약성 정보 서버에 접근하지 못한 NU1900만 있었다. 전체 실행 시간은 1분 44초이다.

## EventHub 비트 조건 경로

2026-11-11에 EventHub의 ulong Builder에 ForFlags 확장 메서드를 추가했다. 기존 EventStream·EventPath의 비트 경로를 재사용하며 연결 수명 검사, 리스너별 구독 소유권과 생애주기 알림을 유지한다. 작업 전 존재하던 Handle·EventHub Connection 정리와 관련 문서·테스트 변경을 보존했다.

Windows·.NET SDK 10.0.403에서 C# 9·netstandard2.1 본체와 net10.0 테스트를 Release로 검증했다. 새 EventHubFlagsTests는 NUnit 직접 사례 14개 메서드·22개 실행 조합이다.

| 실행 범위 | 결과 | 실패 | 건너뜀 |
| --- | ---: | ---: | ---: |
| 본체 솔루션 Release 빌드 | 성공, 경고 1·오류 0 | 0 | 해당 없음 |
| EventHubFlagsTests | 22 / 22 통과 | 0 | 0 |
| 기존 EventHubTests와 새 플래그 테스트 | 136 / 136 통과 | 0 | 0 |
| 전체 테스트 프로젝트 | 919 / 919 통과 | 0 | 0 |

전체 테스트 실행 시간은 1분 31초이다. 기존 랜덤 연속 테스트 30개도 모두 통과했으며 seed별 시간과 실행 이력은 전체 TRX에 포함된다. 빌드 경고는 api.nuget.org 취약성 정보 조회 실패인 NU1900이다.

LogStateProjectTests의 seed 0·1·2는 각각 24.186초·24.024초·24.130초이며 합계 72.340초이다. 대상별 합계는 실행 완료 후 TRX에서 확인했으므로 실행 중 60초 초과를 확인해 중간 질의하지는 못했다.

### 확인한 계약

- IEventHub&lt;ulong&gt;의 Connect 반환값에서 HasAny 판정, 원래 값 전달, 부호 비트 폭과 ulong 최상위 비트를 확인했다.
- 정상 연결의 잘못된 마스크와 null Builder 거부, 기존 구독 유지, 연결 해제·재연결·허브 폐기의 예외 우선순위를 확인했다. 별도 Builder 구현의 NotSupportedException은 코드 검토로 확인했으며 직접 실행 사례에는 포함하지 않았다.
- 서로 다른 enum의 같은 마스크 뒤에서 공통 Where가 한 번 평가되는지와 FlagsNode 묶음 순서를 확인했다. Where로 대신 필터링하는 구현과 구분되는 검증이다.
- Where·Debug·ForFlags 조합, 기존 루트 보존, 콜백 형식별 등록, 전파 중 추가·해제, 실패한 빈 연결의 재연결, 수면과 마스크에 무관한 생애주기 알림을 확인했다.
- 별도 인스턴스 예열 뒤 새 허브의 첫 갱신과 10,000회 반복 갱신 모두 관리 힙 할당 0 B였다. 등록 시점, Debug와 object 박싱 비용은 측정에서 제외했다.

### 실행 과정과 산출물

첫 샌드박스 실행은 testhost 시작 뒤 응답이 없어 중단하고 일반 실행 환경에서 다시 실행했다. 최초 EventHub 실행과 할당 테스트 단독 실행에서 반복 구간에 24 B가 측정되었다. 기존 EventStream 할당 테스트처럼 예열·첫 갱신·반복 갱신에 공통 NoInlining 발행 메서드를 사용하도록 측정 코드를 수정했다. 측정 대상은 새 허브이고 0 B 단언도 유지했다. 제품 코드를 추가 변경하지 않은 최종 EventHub 실행과 전체 실행에서는 첫·반복 구간 모두 0 B였다. 최초 차이의 런타임 원인을 별도 프로파일러로 확정하지는 않았다.

```powershell
dotnet build src/UniEngine.sln -c Release --no-restore
dotnet test tests/UniEngine.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~EventHub --logger 'trx;LogFileName=EventHubFinal.trx' --results-directory tests/bin/TestResults/EventHubForFlags
dotnet test tests/UniEngine.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=All.trx' --results-directory tests/bin/TestResults/EventHubForFlags
```

최종 결과는 [EventHubFinal.trx](../../bin/TestResults/EventHubForFlags/EventHubFinal.trx)와 [All.trx](../../bin/TestResults/EventHubForFlags/All.trx)이다. 최초 실패 기록은 같은 폴더의 EventHub.trx·FlagsAllocation.trx이며 통과 수에 포함하지 않는다. TRX는 버전 관리에서 제외하는 tests/bin 아래의 실행 산출물이다.

API·사용 예제와 [상태·동작 표](../03-E.2-EventHub/docs/01.%20Tables.md)·[테스트 계획](../03-E.2-EventHub/docs/02.%20Plans.md)을 갱신했다. 제품·테스트 C# 파일 3개는 LF이며, 변경한 안내 문서 8개의 상대 링크 53개가 유효했다. Unity 실행과 별도 성능 벤치마크는 수행하지 않았다.

## EventHubBuilder 허브 의존성 분리

2026-11-11에 EventHub의 중첩 Builder를 독립 형식 EventHubBuilder&lt;TEventArgs&gt;로 옮겼다. Builder는 IEventHubConnection과 EventPath만 보관하며, 허브 접근은 연결 계약을 구현한 Connection이 맡는다. Connect의 반환 인터페이스와 Where·Debug·ForFlags·콜백 등록 방식은 유지한다. 기존 구체 형식 EventHub&lt;TEventArgs&gt;.Builder를 직접 참조하는 코드는 새 형식으로 변경해야 한다.

Windows·.NET SDK 10.0.403에서 C# 9·netstandard2.1 본체와 net10.0 테스트를 Release로 검증했다. 기존 테스트와 기대값을 그대로 사용했으며, 전체 실행에 포함된 EventHub 사례를 TRX에서 집계했다.

| 실행 범위 | 결과 | 실패 | 건너뜀 |
| --- | ---: | ---: | ---: |
| 본체와 테스트 프로젝트 Release 컴파일 | 성공, NU1900 경고 | 0 | 해당 없음 |
| 전체 실행에 포함된 EventHub 사례 | 136 / 136 통과 | 0 | 0 |
| 전체 테스트 프로젝트 | 919 / 919 통과 | 0 | 0 |

테스트 실행기가 보고한 전체 실행 시간은 1분 47초이다. LogStateProjectTests의 랜덤 연속 테스트 seed 0·1·2는 각각 31.150초·18.128초·35.884초이며 합계 85.162초이다. 테스트 안내의 중간 확인 기준에 따라 사용자에게 전체 검증을 끝까지 진행하도록 확인받았다. seed별 실행 이력과 결과는 아래 TRX에 포함된다.

첫 샌드박스 실행은 testhost 시작 이후 결과가 나오지 않아 중단했다. 일반 실행 환경에서 다시 실행한 결과만 통과 수에 포함한다. NU1900은 api.nuget.org의 패키지 취약성 정보를 조회하지 못한 경고이다.

```powershell
dotnet test tests/UniEngine.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=All.trx' --results-directory tests/bin/TestResults/EventHubBuilderDecoupling
```

[전체 실행 결과](../../bin/TestResults/EventHubBuilderDecoupling/All.trx)에서 연결 수명과 예외 우선순위, 루트·파생 경로 공유, typed·object·인자 없는 콜백의 구분, ForFlags 조합, 생애주기와 기존 무할당 전파 사례의 통과를 확인했다. Builder의 실행 코드에서 EventHub 구체 형식 참조가 없고 변경한 C# 파일 4개가 LF임을 확인했다. Unity 실행과 별도 성능 벤치마크는 수행하지 않았다.

## EventHub 조건·콜백 결합형 등록

2026-11-11에 Builder의 경로 구성을 OnUpdated의 조건·콜백 결합형 등록과 ulong 전용 OnFlags로 교체했다. Builder의 모든 등록은 같은 인스턴스를 반환하며, Connection이 불변 EventCondition의 단계를 PathInfo로 연결한다. 등록 중복은 콜백 형식·원본 대리자·조건의 단계별 값으로 판정하고, 노드 공유는 기존 EventStream의 대리자·마스크 규칙을 유지한다. 기존 작업 트리의 EventHub·EventStream 변경을 기준으로 검증했다.

Windows·.NET SDK 10.0.403에서 C# 9·netstandard2.1 본체와 net10.0 테스트를 Release로 실행했다. 아래 모듈 수는 별도 재실행이 아닌 전체 실행의 All.trx에서 집계한 값이다.

| 실행 범위 | 결과 | 실패 | 건너뜀 |
| --- | ---: | ---: | ---: |
| 본체 솔루션과 테스트 프로젝트 Release 빌드 | 성공 | 오류 0 | 해당 없음 |
| EventHubTests | 132 / 132 통과 | 0 | 0 |
| EventHubFlagsTests | 29 / 29 통과 | 0 | 0 |
| EventHubRandomContinuousTests | 3 / 3 통과 | 0 | 0 |
| EventHub 합계 | 164 / 164 통과 | 0 | 0 |
| LifecycleHubTests | 18 / 18 통과 | 0 | 0 |
| EventStream 전체 | 215 / 215 통과 | 0 | 0 |
| 전체 테스트 프로젝트 | 947 / 947 통과 | 0 | 0 |

전체 실행기가 보고한 시간은 2분 57초이다. 빌드에는 api.nuget.org의 패키지 취약성 정보를 조회하지 못한 NU1900 경고가 1건씩 있었으며 컴파일 오류는 없었다. 첫 EventHub 선택 실행은 샌드박스에서 testhost 연결이 90초 뒤 시간 초과되어 사례를 하나도 실행하지 못했다. 이 중단은 테스트 사례 실패와 구분하며, 같은 Release 빌드를 일반 실행 환경에서 전체 실행한 결과가 위 통과 수이다.

반환 참조, null을 포함한 target 비교, predicate·Flags·값 조건·Debug 조합, 비트 조건과 target의 차이, 단축형과 EventCondition 사이의 중복을 확인했다. 당시 값 조건의 비교 람다 공유와 target 단축형의 독립 비교는 사용자 형식의 Equals 호출 횟수로 확인했다. 이 공유 차이는 아래 When 경로 통합 작업으로 대체했다. object 조건·콜백의 원본 대리자 비교, 연결 수명 우선 검사, EH-02의 기존 등록 유지와 직접 Updated -=의 루트 한정 제거도 통과했다. 기존 전파 순서·실패·해제·수면·생애주기와 첫·반복 갱신 및 폐기 순회의 무할당 사례도 통과했다. 등록·object 값 형식 박싱·Debug 로그 비용은 무할당 보장에 포함하지 않는다.

새 EventHub 랜덤 연속 사례는 공개 API의 연결·조건별 등록·중복·해제·재연결·수면·인자 오류·폐기를 누적 검증했다. seed마다 독립 허브로 시작했으며 단계별 이력과 아래 측정값은 All.trx의 테스트 출력에 보관한다.

| seed | 실제 도달 깊이 | 실행시간 | 결과 |
| --- | ---: | ---: | --- |
| 0 | 100 / 100 | 40.987 ms | 통과 |
| 1 | 100 / 100 | 17.730 ms | 통과 |
| 2 | 100 / 100 | 14.698 ms | 통과 |

EventHub 세 seed 합계는 73.415 ms로 중간 확인 기준인 60초 이하였다. 전체 실행에 포함된 기존 LogStateProjectTests의 세 seed는 각각 44.608초·43.304초·57.148초, 합계 145.060초였다. 대상별 측정값은 완료 후 TRX에서 확인할 수 있어 실행 중에는 Log의 60초 초과 시점을 확인하지 못했다. 모든 랜덤 연속 사례는 목표 깊이에 도달했으며 중단하거나 건너뛴 사례는 없다.

```powershell
dotnet build src/UniEngine.sln -c Release --no-restore
dotnet build tests/UniEngine.Tests.csproj -c Release --no-restore
dotnet test tests/UniEngine.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~EventHub --logger 'trx;LogFileName=EventHub.trx' --results-directory tests/bin/TestResults/EventHubConditions
dotnet test tests/UniEngine.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=All.trx' --results-directory tests/bin/TestResults/EventHubConditions
```

[전체 실행 TRX](../../bin/TestResults/EventHubConditions/All.trx)와 [샌드박스 중단 TRX](../../bin/TestResults/EventHubConditions/EventHub.trx)는 버전 관리에서 제외하는 tests/bin 아래에 있다. 모듈 문서, 상위 EventSystem 설명, 테스트 상태·동작 표와 계획을 현재 API에 맞추고 상대 링크를 검사했다. 변경한 제품·테스트 C# 파일 8개의 LF와 변경 범위의 git diff --check를 확인했다. EventStream·EventPath의 공개 API, guidelines 서브모듈, .drawio 및 uniengine-mvp는 이번 작업에서 수정하지 않았다. Unity 실행과 별도 성능 벤치마크는 수행하지 않았다.

## 값 조건의 When 경로 통합

2026-11-11에 EventCondition의 값 조건을 When으로 통일하고, Builder의 OnUpdated(target, callback)도 EventStream의 전용 When 경로를 사용하도록 변경했다. 같은 부모 아래의 동등한 target은 같은 조건 객체를 재사용하거나 별도로 구성하거나 단축형을 사용해도 WhenEntry를 공유한다. 등록 중복 판정과 Flags·OnFlags의 이름·동작은 유지했다.

Windows·.NET SDK 10.0.403 Release에서 본체와 테스트를 컴파일한 뒤 선택 실행과 전체 실행을 완료했다. NU1900은 api.nuget.org의 패키지 취약성 정보 조회 경고이며 컴파일 오류는 없었다.

| 실행 범위 | 통과 / 실행 | 실패 | 건너뜀 |
| --- | ---: | ---: | ---: |
| When·EventHub 선택 실행 | 254 / 254 | 0 | 0 |
| 전체 테스트 프로젝트 | 1,028 / 1,028 | 0 | 0 |
| 전체 실행 중 EventHubTests | 140 / 140 | 0 | 0 |
| 전체 실행 중 EventHubFlagsTests | 29 / 29 | 0 | 0 |
| 전체 실행 중 EventHubRandomContinuousTests | 3 / 3 | 0 | 0 |
| 전체 실행 중 EventHub 합계 | 172 / 172 | 0 | 0 |
| 전체 실행 중 LifecycleHub | 18 / 18 | 0 | 0 |
| 전체 실행 중 EventStream | 288 / 288 | 0 | 0 |

각 모듈 수는 전체 실행 TRX에서 집계한 부분집합이다. 전체 실행 시간은 약 80.8초이다. 기존 EventHub 사례에서 값 조건 호출을 When으로 전환했고, 공유 사례를 보강하여 실행 조합은 8개 늘었다.

- 같은 EventCondition 재사용, 별도 동등 조건, target 단축형, 두 형식의 혼합을 typed·인자 없는 콜백 각각에서 검사했다. 사용자 형식의 Equals 호출 횟수로 리스너가 달라도 한 WhenEntry를 공유함을 확인했다.
- null과 별도로 만든 동등한 string target 아래에서 같은 Where 대리자가 한 번만 평가되는지 확인했다. 한 리스너의 연결을 끊어도 다른 구독과 공유 경로가 유지됐다.
- 단축형과 별도 조건 사이의 중복 거부, 다른 callback 형식의 구분, Where·When·Flags·Debug 조합, 연결 수명과 실패·수면·생애주기 및 기존 할당 검증을 유지했다.

| EventHub seed | 실제 도달 깊이 | 실행시간 | 결과 |
| --- | ---: | ---: | --- |
| 0 | 100 / 100 | 8.127 ms | 통과 |
| 1 | 100 / 100 | 2.322 ms | 통과 |
| 2 | 100 / 100 | 2.241 ms | 통과 |

EventHub 세 seed의 실제 실행시간 합계는 12.690 ms이다. 전체 랜덤 연속 테스트 39개는 모두 목표 깊이에 도달했으며, 대상별 세 seed 합계가 60초를 넘지 않았다. 가장 긴 Log는 19.487초·13.863초·21.795초로 합계 55.146초였다. 빌드·복원 시간은 포함하지 않으며 중단하거나 건너뛴 테스트는 없다.

```powershell
dotnet build src/UniEngine.sln -c Release --no-restore -p:UseSharedCompilation=false

dotnet test tests/UniEngine.Tests.csproj -c Release --no-restore -p:UseSharedCompilation=false --filter 'FullyQualifiedName~When|FullyQualifiedName~EventHub' --logger 'trx;LogFileName=Targeted.trx' --results-directory tests/bin/TestResults/EventStreamWhen

dotnet test tests/UniEngine.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=All.trx' --logger 'console;verbosity=normal' --results-directory tests/bin/TestResults/EventStreamWhen *> tests/bin/TestResults/EventStreamWhen/All.console.log
```

[선택 실행 TRX](../../bin/TestResults/EventStreamWhen/Targeted.trx) · [전체 실행 TRX](../../bin/TestResults/EventStreamWhen/All.trx) · [전체 실행 로그](../../bin/TestResults/EventStreamWhen/All.console.log)에 사례 결과와 seed별 이력을 남겼다. 테스트는 testhost 통신이 가능한 일반 실행 권한으로 실행했다. When의 상세 계약, 신규 할당·UniTest 결과와 이번 변경 파일 전체 목록은 [EventStream 검증 결과](../02-E.3-EventStream/docs/03.%20Results.md)에 있다.

성능 벤치마크, PathInfo 크기 측정과 Unity Mono·IL2CPP의 실행·할당 검증은 수행하지 않았다. 등록 후 target의 Equals·GetHashCode 결과가 바뀌는 경우는 지원하지 않는다. 후속 요청에 따라 기존 변경도 함께 커밋하며 .drawio 내용은 편집하지 않았다. 컨벤션 서브모듈은 별도 원본 저장소의 최신 작업 내용으로 맞추고 자체 커밋은 만들지 않는다.

# LifecycleHub·EventHub 검증 결과

EventHub 경로 이전 당시의 통합 검증과 두 Hub 모듈 분리 이후의 검증을 함께 기록한다. 모듈 분리 이전의 EventHubTests 통과 수에는 LifecycleHub 사례가 포함되어 있다.

## 목차

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

# 이전 순회 구현 발췌

2026-11-12에 실험 브랜치를 정리하면서, [설계 결정](Design%20Decisions.md)의 유지보수 비용 비교에 필요한 두 부분을 남긴 자료이다. 아래 코드는 당시 구현에서 발췌한 것으로, 전체 형식과 보조 코드는 포함하지 않는다. 바깥 형식의 들여쓰기만 제거했다. 측정 수치는 [벤치마크](Benchmark.md)에 있다.

## 목차

- 배열의 지연 압축
- 표의 스택 순회

## 배열의 지연 압축

출처는 cab9d8e1906a4b94742ef59ec15e18ac2f61c6b9의 src/02-Core/02-E.3-EventStream/EventGraph/Slots.cs이다.

삭제는 배열 칸을 비우고, 가장 바깥 순회가 끝나면 남은 항목을 앞으로 모은다. 각 항목의 Slot과 빈칸 수, 순회 중인 방문자 수를 함께 관리한다.

```csharp
internal void Remove(TItem item)
{
    if (item.Slot < 0) return;

    _items[item.Slot] = null;
    item.Slot = -1;
    _holes++;

    // An entirely empty idle array can reset its bounds in constant time.
    if (_visitors == 0 && IsEmpty)
    {
        _count = 0;
        _holes = 0;
    }
}

internal void Enter() => _visitors++;

internal void Exit()
{
    if (--_visitors == 0 && _holes != 0) Compact();
}

private void Compact()
{
    var write = 0;
    for (var read = 0; read < _count; read++)
    {
        var item = _items[read];
        if (item == null) continue;

        _items[write] = item;
        item.Slot = write++;
    }

    Array.Clear(_items, write, _count - write);
    _count = write;
    _holes = 0;
}
```

## 표의 스택 순회

출처는 d818fe7924bea7f17c15286cc80522ed621a3e05의 src/02-Core/02-E.3-EventStream/EventGraph/Traversal.cs이다.

방문할 작업은 종류와 ID를 가진 Frame으로 Frame 배열에 넣는다. 같은 파일의 Run은 `while (_stackTop > bottom)`에서 `_stack[--_stackTop]`을 꺼내 처리한다. 재귀 호출이 맡던 다음 작업과 종료 처리를 그래프가 명시적으로 관리한다.

```csharp
private void EnsureStackCapacity(int required)
{
    if (_stack.Length < required) Array.Resize(ref _stack, Math.Max(required, _stack.Length * 2));
}

private void Push(
    FrameKind kind,
    int id,
    Signal signal,
    Exception error = null,
    IGraphObserver<T> input = null)
{
    if (id == None && (kind == FrameKind.Children || kind == FrameKind.Rows || kind == FrameKind.Subscriptions))
        return;

    EnsureStackCapacity(_stackTop + 1);
    _stack[_stackTop++] = new Frame { Kind = kind, Id = id, Signal = signal, Error = error, Input = input };
}
```

Run의 Connection 분기는 자식 방문을 먼저 넣고 구독자 방문을 나중에 넣는다. 마지막에 넣은 작업부터 꺼내므로 구독자가 자식 노드보다 먼저 호출된다.

```csharp
case FrameKind.Connection:
    if (!IsConnected(id)) break;
    Push(FrameKind.Children, ConnectionAt(id).FirstChild, currentSignal);
    Push(FrameKind.Subscriptions, ConnectionAt(id).FirstSubscription, currentSignal);
    break;
```

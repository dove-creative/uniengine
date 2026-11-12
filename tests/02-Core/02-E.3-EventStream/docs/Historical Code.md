# EventStream 이전 비교 구현 코드

[테스트 계획](02.%20Plans.md)의 구조별 비교에 필요한, 현재 소스에 없는 핵심 코드만 보존한다. 아래는 원래 구현의 발췌이며 전체 형식·보조 코드·독립 실행 프로그램을 구성하지 않는다. 바깥 형식의 들여쓰기만 제거한다.

## Filter 인스턴스 공유

출처: cb62332a923c894638395f3460ef4af6ce116120의 src/02-Core/02-E.3-EventStream/Filter.cs이다. 비교용 시나리오는 조건 하나에 Filter 인스턴스 하나를 만들어 공유한다.

```csharp
public sealed class Filter<T>
{
    // Front
    public string Name { get; }

    // Internal
    internal Func<T, bool> Predicate { get; }


    // Content
    public Filter(string name, Func<T, bool> predicate)
    {
        Name = name;
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }
}
```

같은 커밋의 src/02-Core/02-E.3-EventStream/EventGraph.cs에서 선형 검색은 참조 동일성을 사용한다.

```csharp
for (var child = parent.Children.First; child != null; child = child.Next)
{
    if (child.Path?.Operation == PathOperation.Where && ReferenceEquals(child.Path.Filter, filter))
        return child;
}
```

## 자식 OOP의 재귀 순회

출처: 5478bbd0c39a4ad09cea01faa4998f38c4ba3442의 src/02-Core/02-E.3-EventStream/EventGraph/Node.cs에 있는 NodeBase.Visit이다.

```csharp
internal override void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure)
{
    for (var child = Children.First; child != null; child = child.Next)
    {
        if (child.Parent != null && child.Registration <= lastVisible)
            child.Visit(value, lastVisible, ref failure);
    }
}
```

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

[테스트 계획](02.%20Plans.md)

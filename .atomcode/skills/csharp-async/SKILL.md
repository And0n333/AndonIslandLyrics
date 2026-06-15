---
name: csharp-async
description: C# 异步编程最佳实践 — async/await、Task、CancellationToken、并发模式
disable_model_invocation: true
---

# C# Async/Await 最佳实践

## 基础规则

### async/await
- 全程异步：避免 `async void`（事件处理除外）
- 避免 `.Result` / `.Wait()` — 会导致死锁（WPF 的 SynchronizationContext）
- 使用 `ConfigureAwait(false)` 在库代码中（UI 层保留 `true`）
- 异步方法统一 `Async` 后缀命名

### Task 管理
- `Task.WhenAll()` 并行执行独立异步操作
- `Task.WhenAny()` 用于超时或竞态场景
- `CancellationToken` 始终从最顶层传播到底层
- `ValueTask` 适合高频同步路径，但注意只能 await 一次

### 资源释放
- `IAsyncDisposable` 用于异步清理（如网络流）
- HttpClient 应长期复用，不要在每个请求时 `new HttpClient()`
- SemaphoreSlim 限制并发量

## WPF 特殊考虑

### UI 线程
- `async void` 事件处理在 UI 线程启动，await 后回到同一上下文
- 不要用 `Task.Run` 做 UI 更新操作
- 用 `Dispatcher.InvokeAsync` 在非 UI 线程中更新属性

### 常见死锁场景
```csharp
// BAD — 死锁！
var result = DoAsync().Result;

// GOOD
var result = await DoAsync();
```

### CancellationToken 模式
```csharp
public async Task DoWorkAsync(CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();
    await SomeOperationAsync(ct);
}
```

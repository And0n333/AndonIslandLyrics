# /format — 格式化代码

执行 `dotnet format` 统一 C# 代码风格。

## 用法

```
/format
/format [severity]
```

## 参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `severity` | 修复级别：`info`、`warning`、`error` | `warning` |

## 示例

```
/format
/format error
```

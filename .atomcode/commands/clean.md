# /clean — 清理构建产物

删除 `bin/` 和 `obj/` 目录，解决构建缓存问题。

## 用法

```
/clean
```

## 说明

相当于执行：
```
rmdir /s /q bin obj
```

清理后需要重新 `dotnet restore` 和 `dotnet build`。

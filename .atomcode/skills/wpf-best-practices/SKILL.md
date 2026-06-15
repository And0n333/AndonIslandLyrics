---
name: wpf-best-practices
description: WPF 最佳实践 — 绑定、性能、内存管理、样式、自定义控件
disable_model_invocation: true
---

# WPF 最佳实践

## 绑定与 MVVM

### 绑定性能
- 使用 `x:Static` 而非 `{Binding}` 访问静态属性
- UI 频繁更新时使用 `BindingMode.OneWay` 而非默认模式
- 列表数据用 `ObservableCollection<T>`，批量更新用 `AddRange` 扩展
- 大型列表使用虚拟化 `VirtualizingStackPanel`

### INotifyPropertyChanged
- 使用 `CallerMemberName` 自动获取属性名
- 对比旧值再触发事件，避免不必要更新
- 批量更新时使用 `BeginInit`/`EndInit`

## 样式与资源

### 资源字典
- 共享样式放入 `App.xaml` 或独立资源字典
- `StaticResource` 用于不变资源，`DynamicResource` 用于运行时切换主题
- 避免在 `DataTemplate` 中定义大量资源

### 自定义控件
- 使用 `TemplateBinding` 链接控件属性到模板
- 触发器优先于转换器
- 合理使用 `VisualStateManager` 而非纯触发器

## 内存管理

### 避免内存泄漏
- 事件注销：订阅了的事件必须取消订阅
- `Loaded`/`Unloaded` 成对使用
- `WeakEvent` 模式用于长生命周期对象（静态事件）
- `Binding` 在移除元素前清理绑定（`BindingOperations.ClearBinding`）

### 图片资源
- 使用 `BitmapCacheOption.OnLoad` 控制加载时机
- 大图使用 `BitmapFrame.Create` 的降采样参数
- 及时释放 `ImageSource` 资源

## 透明窗口

### 性能
- `AllowsTransparency=True` 启用硬件加速渲染
- 避免在透明窗口上放置复杂视觉效果
- `WindowStyle=None` + `ResizeMode=CanResizeWithGrip` 配合使用
- 使用 `DropShadowEffect` 时控制 BlurRadius 不要过大

### 拖动
- `DragMove()` 需要在 `MouseLeftButtonDown` 事件中调用
- 检查 `e.ClickCount == 2` 处理双击与拖动的区分

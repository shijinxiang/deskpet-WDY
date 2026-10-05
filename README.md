# TranslatePet - Windows 翻译桌宠

一款运行于 Windows 桌面、以轻量桌宠为交互入口的屏幕截图 OCR 翻译工具。

![Architecture](https://img.shields.io/badge/.NET-8.0%20LTS-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-brightgreen)
![Tests](https://img.shields.io/badge/Tests-Passing-success)

---

## 📖 核心特性

- **轻量桌面宠物悬浮入口**：
  - 灵动的动态呼吸与眨眼微交互，柔和半透明精灵造型。
  - 随时拖拽至屏幕任意位置，坐标自动持久化记忆。
  - 单击即可唤起截图翻译；右键支持呼出功能菜单；支持最小化到系统托盘常驻。
- **全局快捷键随时唤起**：
  - 默认快捷键 `Alt + Q`（支持在设置中心自定义组合键）。
  - 快捷键冲突检测与托盘气泡提示。
- **跨多显示器、高 DPI 选区遮罩**：
  - 支持多屏、副屏负坐标布局与不同缩放比例（PerMonitorV2 DPI 感知）。
  - 截图前自动瞬时隐去桌宠，绝不遮挡或污染目标识别画面。
  - 拖拽实时暗色遮盖，选区高亮展示，带像素尺寸标签；支持 `Enter` / 双击快速确认，`Esc` / 右键随时取消。
- **纯内存处理与隐私保障**：
  - 截取的画面仅在内存字节流中流转与裁剪，**默认绝不保存到本地磁盘**，流程结束立即释放。
- **OCR 识别结果即时校验与编辑**：
  - 识别出的文字支持检查、手动编辑修改与一键复制原文（解决模糊字、排版换行等识别偏差）。
- **多通道翻译服务**：
  - **OpenAI 兼容接口**：全面兼容 DeepSeek、OpenAI、Moonshot、Qwen、本地 Ollama 等大模型 API。
  - **公共免密翻译通道**：内置 MyMemory 公共翻译接口，开箱即用，无需配置 API Key 即可快速体验。
  - **云端通用 OCR**：内置百度智能云 OCR（支持通用文字识别高精度 API），并预留离线/本地扩展接口。
- **架构扩展契约**：
  - 已按规范预留通用 `ITtsService` 语音播报契约，服务边界清晰解耦。

---

## 🏗️ 架构与模块划分

```text
TranslatePet.sln
├── src/
│   ├── TranslatePet.Core/              # 核心业务逻辑与模型（无 UI 依赖）
│   │   ├── Models/                     # ScreenRect, OcrResult, TranslationResult, AppSettings
│   │   ├── Interfaces/                 # ICaptureService, IOcrService, ITranslationService, ITtsService, ISettingsService
│   │   └── Services/                   # SettingsService, TranslationWorkflow
│   │       ├── Ocr/                    # BaiduCloudOcrProvider, MockOcrProvider, OcrService
│   │       └── Translation/            # OpenAiCompatibleTranslationProvider, PublicFallbackTranslationProvider, TranslationService
│   │
│   └── TranslatePet.UI/                # WPF 桌面呈现层 (.NET 8-windows)
│       ├── Utils/                      # NativeMethods (Win32), VirtualScreenHelper (多屏/负坐标)
│       ├── Services/                   # WpfCaptureService (内存快照), GlobalHotkeyHelper (Win32全局热键)
│       ├── Tray/                       # TrayIconManager (原生托盘图标与右键菜单)
│       ├── Views/                      # 窗口视图
│       │   ├── PetWindow.xaml          # 桌宠悬浮窗 (动画、拖拽、右键菜单)
│       │   ├── SnippingOverlayWindow   # 跨屏选区遮罩 (选区矩形、尺寸标签、确认/取消)
│       │   ├── ResultWindow.xaml       # 原文编辑、语言切换、译文展示、一键复制
│       │   └── SettingsWindow.xaml     # OCR / 大模型 API / 快捷键偏好设置
│       └── app.manifest                # 声明 PerMonitorV2 高 DPI 感知
│
└── tests/
    └── TranslatePet.Tests/             # xUnit 单元测试工程 (测试覆盖模型、工作流、配置持久化)
```

---

## 🚀 编译与运行

### 环境要求
- Windows 10 (1809+) 或 Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 编译项目
```powershell
dotnet build
```

### 运行单元测试
```powershell
dotnet test
```

### 启动运行应用
```powershell
dotnet run --project src/TranslatePet.UI/TranslatePet.UI.csproj
```

---

## ⚙️ 快捷操作说明

| 动作 | 交互方式 |
|---|---|
| **启动截图翻译** | 点击桌宠身体，或按下全局快捷键 `Alt + Q`，或托盘菜单选择“截图翻译” |
| **完成框选** | 鼠标左键拖拽选区后，按 `Enter` 键或双击选区，或点击浮动工具栏“确认识别” |
| **取消框选** | 按 `Esc` 键或点击鼠标右键，或点击浮动工具栏“取消” |
| **拖动桌宠** | 鼠标左键按住桌宠并拖拽至任意桌面位置，松开即保存 |
| **打开设置** | 鼠标右键点击桌宠或右键点击托盘图标，选择“参数设置” |
| **关闭翻译卡片** | 按 `Esc` 键或点击卡片右上角 `✕` |

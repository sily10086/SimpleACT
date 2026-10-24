# Unity 操作规范

本文规定 Agent 在任务确实需要与 Unity Editor 或开发版 Player 交互时的工具选择、操作边界与收尾要求。

## 一、适用范围

本规范仅适用于需要操作 Unity Editor 或开发版 Player 的工作，包括：

- 通过 Unity 刷新资源、编译脚本或验证代码修改。
- Scene、Prefab、Material、ScriptableObject 等 Unity 序列化内容的创建和修改。
- GameObject、Component、Transform 和序列化字段操作。
- 美术、音频、字体、模型、动画等外部资源的导入与导入设置。
- Unity 工程设置、构建设置、Tags、Layers、Quality、Graphics、Player Settings 等配置操作。
- Unity Editor 或开发版 Player 的运行、调试、截图、日志检查和验证。
- `Assets/` 目录下资源的移动、重命名、复制和删除。

以下工作不会单独触发本规范：

- 阅读、分析或评审 C# 代码。
- 仅创建或修改普通 `.cs` 源码。
- 仅修改 Shader、CSV、JSON、XML、YAML、Markdown 等普通文本文件。
- 不连接、不控制 Unity Editor 或开发版 Player 的其他纯文本开发工作。

如果纯代码任务后续需要通过 Unity Editor 刷新、编译、检查 Console、进入 Play Mode 或执行其他编辑器验证，则从准备进行 Unity 交互时开始读取并遵循本规范。

本规范只规定 Unity 项目的操作方式，不替代项目已有的需求确认、代码设计、Git、安全和修改后验证规范。

“可直接编辑”只表示可以使用文件编辑工具完成，不代表可以跳过修改前确认流程。涉及项目修改时，仍必须先取得对应修改授权。

## 二、核心工具要求

### 1. 统一使用 Unity CLI/Pipeline

- 与 Unity Editor 或开发版 Player 交互时，统一通过 Unity CLI/Pipeline 完成。
- 禁止使用 Unity MCP。
- 与 Unity 交互时必须使用 `unity-pipeline` 技能。
- 使用 Pipeline 前必须完整阅读并遵循 `unity-pipeline` 技能中的命令、参数、目标选择、状态轮询、错误处理和收尾流程。
- 不得根据历史经验假定 Pipeline 一定支持某项操作。
- 必须以当前目标实例实际返回的 `unity command` 列表为准。
- 如果当前实例没有所需专用命令，应先判断是否可以使用最小范围的 `eval` 完成。
- Pipeline 和 `eval` 都不能可靠完成时，应停止相关 Unity 内容操作并向用户说明限制，不得绕过规范直接修改序列化文件。
### CLI 连接与实例发现

`com.unity.pipeline` 由 Unity CLI 驱动。调用前先从目标项目目录发现实例和命令，不得凭历史记录猜测命令或端口：

```powershell
unity command --project-path "...\UnityProject"
unity command --project-path "...\UnityProject" editor_status
```

不带命令名的调用用于发现目标实例并返回当前实例实际支持的 `unity command` 列表。存在多个 Editor 时，必须显式指定 `--project-path` 或 `--instance`；无法区分时暂停操作。

开发版 Player 使用运行时目标参数，参数位置必须在 `command` 之后、命令名之前：

```powershell
unity command --runtime MyGame.exe runtime_status
unity command --runtime-path "C:\Builds\MyGame" runtime_status
```

Editor 默认使用 `7800-7849` 端口范围，Runtime 默认使用 `7900-7949`。实例描述文件分别位于 `Library/Pipeline/.unity-pipeline-port` 和运行目录下的 `.unity-pipeline-runtime-port`。客户端应通过 CLI 或描述文件发现 `127.0.0.1`、端口和认证信息，不应将端口或 token 写死。

### 命令发现、Schema 与异步状态

首次连接后，优先读取当前实例返回的命令列表；参数不确定时查询该命令的 live schema 或 `--help`。命令名和参数以目标实例为准，包版本文档只作为参考。

命令响应可能是同步结果、`queued`/`in_progress` 或错误状态。异步结果必须使用对应的 `*_status` 命令轮询到 `completed`、`up_to_date`、`failed` 或 `canceled`。冷启动时先等待 Editor 从 `settling` 进入 `ready`。
当前项目通过 `Packages/manifest.json` 使用 `com.unity.pipeline` `0.7.0-exp.1`。涉及具体命令参数时，优先读取目标项目实际安装包的 `Documentation~/`；如果包来自注册表且源码文档不在仓库，则以目标实例返回的命令列表和 live schema 为最终依据。可参考 Unity 官方包文档：<https://docs.unity3d.com/Packages/com.unity.pipeline@0.5/manual/index.html>。文档版本与项目版本不一致时，不得直接假定命令或参数兼容。

### 2. Pipeline 不可达时的处理

当 Unity Pipeline 不可连接、`unity-pipeline` 技能不可用、目标实例不可识别或命令执行状态无法确认时：

允许继续的操作：

- 阅读和分析项目文件。
- 搜索代码、配置和资源引用。
- 修改普通 `.cs` 源码。
- 修改 Shader、CSV、JSON、XML、YAML、Markdown 等明确属于纯文本且不由 Unity 序列化管理的文件。
- 给出 Scene、Prefab、Material 等内容的修改方案。

禁止继续的操作：

- 创建或修改 Unity 序列化资源。
- 修改场景对象、Prefab 层级、组件和序列化字段。
- 修改资源导入设置。
- 移动、重命名、复制或删除 `Assets/` 下的既有资源。
- 修改 Unity 工程设置。
- 用文本编辑器绕过 Pipeline 修改 `.unity`、`.prefab`、`.mat`、`.asset` 等文件。

如果因 Pipeline 不可达而无法完成必要验证，最终回复必须明确说明：

- 哪些修改已经完成。
- 哪些 Unity 操作没有执行。
- 哪些验证没有完成。
- 未验证内容可能影响的范围。

不得把未经过 Unity 刷新、编译、加载或序列化验证的结果描述为已经完整验证。


## 自然语言意图与 Pipeline 命令映射

以下短语是 Codex/Skill 的语义触发词，不是 Pipeline 命令名。触发后必须先发现实例，再根据具体意图选择命令：

| 用户意图 | Pipeline 命令或流程 |
|---|---|
| `Unity`、`in Unity`、`操作 Unity` | 读取项目规则，执行实例发现和 `editor_status`，不因关键词本身直接修改内容 |
| `Unity Pipeline`、`automate Unity`、`editor automation`、`Unity Editor 自动化`、`Unity 编辑器`、`Unity 技能` | 加载 `unity-pipeline` Skill，按当前实例命令列表执行 |
| `创建脚本` | `create_script` -> `recompile` -> 轮询 `recompile_status` -> `attach_script`（如需挂载） |
| `场景分析` | `editor_status`、`list_open_scenes`、`get_scene_hierarchy`、`find_gameobjects`、属性读取和必要的 `capture_scene_view` |
| `build scene`（制作场景） | `create_scene`/`open_scene` -> `create_gameobjects` -> `set_parent`/`set_transform` -> 组件/Prefab 命令 -> `save_scene` |
| `build scene`（构建 Player） | `get_build_settings` -> `build` -> 轮询 `build_status` |
| `管理资源` | `find_assets`、`create_asset`、`import_asset`、`copy_asset`、`move_asset`、`rename_asset`、`delete_asset`、导入设置命令 |
| `全自动模式` | 在用户已明确授权任务范围后自动编排命令和轮询；仍遵守 `confirm`、目标确认、范围和破坏性操作边界 |
| `半自动模式` | 自动执行只读查询和 `dry_run`，在真实写入、覆盖、删除、批量导入、构建或平台切换前请求确认 |

`build scene` 必须先区分“场景制作”和“Player 构建”，不能映射到一个不存在的固定命令。`Unity` 这类过宽触发词只触发上下文确认，不代表可以直接改 Scene 或资源。

### 创建脚本的标准流程

`create_script` 不会立即让新类型可用。创建脚本后必须重新编译并轮询完成，再执行挂载或组件字段操作：

```powershell
unity command --project-path "...\UnityProject" create_script ...
unity command --project-path "...\UnityProject" recompile
unity command --project-path "...\UnityProject" recompile_status
unity command --project-path "...\UnityProject" attach_script ...
```

### 场景、资源和构建的命令边界

- 场景分析使用只读查询，不自动保存或修改 Scene。
- 场景制作使用专用 Scene、GameObject、Component、Prefab 命令；相关修改通过 Unity Undo 和正确的脏标记持久化。
- 资源操作先查询目标，再按需要使用 `dry_run=true`；覆盖、删除和破坏性操作必须使用 `confirm=true`。
- Player 构建是独立的异步流程，不等同于创建或修改 Scene。
## 三、操作前检查

进行任何 Unity 操作前，必须完成以下检查。

### 1. 阅读项目规则

- 查看项目根目录及相关子目录中的 `AGENTS.md`。
- 查看与当前功能对应的设计文档、开发说明、资源规范和测试说明。
- 检查当前可用技能，并完整阅读 `unity-pipeline` 技能。
- 搜索项目中是否已有相同或相近的实现方式。
- 优先复用项目已有工具、组件、资源组织方式和操作流程。

### 2. 确认 Unity 项目和目标实例

- 确认当前目录属于目标 Unity 项目。
- 确认项目路径，避免连接到其他项目的 Editor。
- 查询当前可连接的 Unity Editor 和开发版 Player。
- 只有一个目标实例时，可以使用该实例，但仍应核对项目和运行状态。
- 存在多个可连接实例时，必须显式指定目标实例。
- 不得仅凭窗口标题、最近使用记录或实例返回顺序选择目标。
- 无法可靠区分多个实例时，必须暂停并让用户确认。

### 3. 确认目标状态

根据任务需要检查：

- Unity Editor 是否处于 Edit Mode 或 Play Mode。
- 当前打开的 Scene。
- Scene 是否存在未保存修改。
- Prefab Stage 是否打开。
- Unity 是否正在编译、导入资源、刷新 AssetDatabase 或切换构建目标。
- Console 是否已有历史错误。
- 当前目标是否具备所需 `unity command`。

不得在不了解目标状态时直接执行场景切换、退出 Play Mode、刷新资源、保存场景或覆盖资源等可能影响用户现场的操作。

### 4. 尊重用户未保存内容

- 不得擅自保存用户已有的未保存 Scene、Prefab 或其他编辑器内容。
- 不得擅自丢弃用户已有的未保存修改。
- 如果任务需要切换 Scene、进入或退出 Play Mode、关闭 Prefab Stage，且可能影响未保存内容，应先说明影响并等待用户确认。
- 无法区分哪些未保存内容属于当前任务时，不得自行保存或丢弃。

## 四、可直接编辑的文件

当任务因包含 Unity Editor 或开发版 Player 操作而已经命中本规范时，以下内容仍可以使用普通文件编辑工具直接创建或修改：

- 普通 `.cs` 源码。
- `.shader`、`.shadergraph` 之外的纯文本 Shader 源码。
- CSV、JSON、XML、Markdown 和普通文本配置。
- 项目明确说明可以手工维护的其他纯文本文件。

直接编辑时必须遵循：

- 保持原有编码、换行、缩进和代码风格。
- 默认使用 UTF-8。
- 不修改无关代码。
- 不对整个文件做无意义格式化。
- 不手动创建或修改对应 `.meta`。
- 新文件写入 `Assets/` 后，由 Unity 负责生成 `.meta` 和完成导入。
- 涉及多个脚本或文本文件时，应先批量完成修改，再统一执行必要的 Unity 刷新、编译和验证。
- 不应每修改一个脚本就单独触发一次刷新或等待编译，避免重复 Domain Reload、资源导入和程序集编译。

本节不表示普通 C# 修改会单独触发本规范。若任务同时修改 C# 代码并计划通过 Unity Editor 验证，应根据风险执行必要验证：

- 让 Unity 刷新并编译脚本。
- 轮询编译状态，不能只依赖固定时间等待。
- 检查本轮新增的编译错误和相关 Console 日志。
- 修改公共接口、序列化字段、组件类或程序集结构时，检查资源绑定和调用方兼容性。

## 五、必须通过 Pipeline 操作的内容

### 1. Unity 序列化内容

创建或修改以下内容时必须使用 Pipeline：

- Scene：`.unity`
- Prefab：`.prefab`
- Material：`.mat`
- ScriptableObject 和其他序列化资产：`.asset`
- AnimationClip：`.anim`
- AnimatorController：`.controller`
- AnimatorOverrideController
- Timeline 和 PlayableAsset
- Lighting、Volume Profile、Renderer Data 等 Unity 管理资产
- Unity 管理的其他序列化资源

禁止通过文本替换、正则表达式、脚本直接改写文件内容或手工拼接 YAML 完成这些操作。

### 2. 场景对象和 Prefab

以下操作必须使用 Pipeline：

- 创建或删除 GameObject。
- 修改父子层级和兄弟顺序。
- 修改 Transform。
- 添加、移除或替换 Component。
- 修改组件序列化字段。
- 设置对象激活状态。
- 修改 Tag、Layer、Static 标记。
- 创建或应用 Prefab。
- 修改 Prefab 实例 Override。
- 打开、编辑、保存或退出 Prefab Stage。
- 修改 Scene 中的资源引用和对象引用。

场景、Prefab、材质和视觉效果等内容必须在 Unity Editor 中正确制作并持久化。

不得使用以下方式代替资产制作：

- 在运行时代码中临时创建本应存在于 Scene 或 Prefab 中的对象。
- 使用运行时 `AddComponent` 补齐编辑器中缺失的组件。
- 使用 `Transform.Find` 或递归查找作为 UI 或场景绑定兜底。
- 使用硬编码资源路径绕过缺失引用。
- 在启动时用代码强行覆盖本应序列化保存的配置。

### 3. Unity 工程设置

以下内容必须使用 Pipeline 或 Unity 提供的专用 API 修改：

- Build Settings。
- Player Settings。
- Tags 和 Layers。
- Sorting Layers。
- Input 设置。
- Quality Settings。
- Graphics Settings。
- Physics 设置。
- Time 设置。
- Audio 设置。
- Package 或项目级 Unity 配置。
- 其他由 Unity 管理的 Project Settings。

不得直接修改 `ProjectSettings/` 下的序列化文件，除非项目文档明确允许且用户明确授权该操作。

### 4. 资源导入和导入设置

以下操作必须使用 Pipeline：

- 触发 Unity 资源导入。
- 设置 Texture、Model、Audio、Animation、Font 等 Importer 参数。
- 设置平台覆盖参数。
- 设置 AssetBundle、Addressables 或其他 Unity 资源标签。
- 检查导入结果、子资源和依赖关系。
- 验证 Unity 是否正确识别外部资源。

外部源文件可以先复制到目标目录，但必须遵循：

- 复制前检查目标路径。
- 检查是否会覆盖已有文件。
- 检查同名资源和目录结构。
- 不带 `.meta` 的新资源复制到目标目录后，由 Unity 刷新并生成 `.meta`。
- 外部资源自带 `.meta` 且确实需要保留 GUID 时，必须连同资源一起复制。
- 复制外部 `.meta` 前必须检查 GUID 是否与项目现有资源冲突。
- 无法确认 GUID 安全时，不得复制外部 `.meta`，应让 Unity 生成新的 `.meta`。
- 导入完成后应检查 Importer 状态、资源类型和关键设置。

批量导入前必须获得用户确认的情况：

- 会覆盖已有资源。
- 会引入或替换大量 `.meta`。
- 会导致大量资源重新导入。
- 会修改多个平台的导入设置。
- 可能改变现有资源 GUID 或引用关系。

### 5. Assets 目录下的资源管理

移动、重命名、复制或删除 `Assets/` 下的既有文件和文件夹时，必须使用 Pipeline 或 Unity AssetDatabase 对应能力。

不得直接使用文件系统工具完成以下操作：

- 移动既有资源。
- 重命名既有资源。
- 删除既有资源。
- 复制需要保留 Unity 引用关系的既有资源。
- 移动或重命名已有文件夹。

原因是这些操作必须由 Unity 同步维护：

- `.meta`。
- GUID。
- 资源引用关系。
- AssetDatabase 状态。
- Importer 状态。
- Prefab、Scene、Material 和 ScriptableObject 中的引用。

删除资源前必须：

- 明确删除目标。
- 查询主要引用和依赖。
- 判断是否影响 Scene、Prefab、Material、配置或代码。
- 获得用户对删除操作的明确授权。

不得为了修复引用问题而删除并重新创建资源，因为这通常会改变 GUID 并扩大影响范围。

## 六、禁止直接操作的内容

以下操作默认禁止：

- 手动创建 `.meta`。
- 手动修改 `.meta`。
- 手动删除 `.meta`。
- 将已有 `.meta` 的 GUID 复制给其他无关资源。
- 在未检查冲突的情况下导入外部 `.meta`。
- 直接修改 `.unity`。
- 直接修改 `.prefab`。
- 直接修改 `.mat`。
- 直接修改 `.asset`。
- 直接修改 `.controller`。
- 直接修改 `.anim`。
- 直接修改其他 Unity 序列化资源。
- 修改 `Library/`。
- 修改 `Temp/`。
- 修改 `Logs/`。
- 修改 `UserSettings/`。
- 修改 Unity 自动生成的 `*.csproj`。
- 修改 Unity 自动生成的 `*.sln`。
- 修改 Unity 缓存、导入数据库或本地编辑器状态文件。
- 以字节方式改写图片、音频、模型、视频或其他二进制资产。
- 通过十六进制、二进制补丁或未知工具修改 Unity 资产。
- 为绕过 Pipeline 不可用而手工编辑 Unity YAML。
- 用运行时代码临时构造本应由编辑器持久化的项目内容。

如果任务确实涉及生成文件或二进制资产，应修改其源文件或使用对应生产工具，然后通过 Pipeline 导入 Unity。

## 七、Pipeline 操作原则

### 1. 以专用命令优先

操作优先级如下：

1. 当前 `unity command` 列表中的专用操作。
2. Pipeline 提供的通用资产或对象操作。
3. 最小范围的 `eval`。
4. 无可靠操作方式时停止并说明限制。

不得在已有专用命令的情况下，为了方便直接使用 `eval`。

### 2. `eval` 使用限制

只有在没有合适专用命令时才可以使用 `eval`。

使用 `eval` 时必须：

- 代码范围仅覆盖当前任务。
- 不创建长期保留的临时编辑器脚本。
- 不引入当前需求之外的通用工具。
- 不扫描或修改无关资产。
- 使用明确的目标路径和对象。
- 必要时接入 Unity Undo。
- 正确设置对象或资源的脏标记。
- 只保存本次任务明确修改的内容。
- 操作后刷新并查询结果。
- 输出足够定位失败原因的上下文。
- 清理当前任务产生的临时对象或测试状态。

不得使用大段 `eval` 绕过项目已有工具、Pipeline 限制或用户确认流程。

### 3. 批量处理

对于同类目标，应采用批量查询、批量修改和统一验证：

- 先一次性查询目标集合。
- 在执行前核对路径、数量和类型。
- 批量执行同类修改。
- 批量完成后统一刷新。
- 统一等待导入或编译结束。
- 统一检查本轮新增错误。
- 必要时按批次截图验证。

避免以下低效操作：

- 对每个对象单独连接 Unity。
- 对每个脚本单独刷新和编译。
- 对每个资源重复查询相同状态。
- 每修改一个字段就保存一次 Scene。
- 在没有状态变化时重复截图或轮询。

批量操作不能牺牲可定位性。执行结果应能明确对应到具体目标，失败项不能被成功项掩盖。

### 4. 状态轮询

等待 Unity 状态变化时，应使用 Pipeline 规定的状态查询或轮询方式。

需要轮询的状态包括：

- 脚本编译。
- AssetDatabase 刷新。
- 资源导入。
- Domain Reload。
- Play Mode 进入或退出。
- Scene 加载。
- 构建。
- 异步 Pipeline 命令。

不得只使用固定时长等待后直接假定操作完成。

轮询必须：

- 使用合理的间隔。
- 设置合理超时。
- 检查成功、失败和取消状态。
- 超时后查询当前状态和错误信息。
- 不在状态未知时继续执行依赖步骤。

### 5. 大范围操作授权

执行以下操作前必须获得用户明确授权：

- 大范围重新导入资源。
- 批量改写 Unity 序列化资源。
- 切换构建目标。
- 构建 Player。
- 构建 AssetBundle 或 Addressables。
- 批量修改 Importer。
- 批量移动、重命名或删除资源。
- 修改多个 Scene 或 Prefab。
- 修改全局工程设置。
- 可能触发长时间编译、导入或构建的操作。
- 可能影响用户当前未保存编辑状态的操作。

授权前应说明：

- 操作目标。
- 影响范围。
- 预计触发的 Unity 行为。
- 是否可能产生大量文件变化。
- 是否可能影响现有资源引用或平台配置。

## 八、脚本兼容与资源绑定

### 1. 序列化字段

修改序列化字段时必须检查：

- 字段是否被 Scene 或 Prefab 序列化。
- 字段重命名是否需要 `FormerlySerializedAs`。
- 字段类型变化是否会导致原数据丢失。
- 字段从实例成员改为静态成员是否破坏序列化。
- 字段可见性变化是否影响 Inspector 和现有绑定。
- 自定义序列化结构变化是否兼容已有数据。

不得仅以“代码可以编译”作为序列化兼容成立的依据。

### 2. 组件类

移动、重命名或修改 `MonoBehaviour`、`ScriptableObject` 等组件类时必须检查：

- 脚本 GUID 是否保持稳定。
- 命名空间变化是否影响类型解析。
- 类名和文件名是否仍符合 Unity 组件绑定要求。
- Scene 和 Prefab 是否出现 Missing Script。
- ScriptableObject 资产是否仍能加载。
- 自定义 Editor 和 PropertyDrawer 是否仍能匹配目标类型。

不得通过删除再新建脚本的方式处理普通重命名，因为这可能改变 `.meta` 和脚本 GUID。

### 3. 程序集结构

修改 `.asmdef`、程序集引用或脚本目录结构时必须检查：

- 程序集依赖方向。
- Runtime 与 Editor 代码边界。
- 平台限制。
- Define Constraints。
- 循环依赖。
- 现有调用方是否仍能访问相关类型。
- Unity 编译顺序和测试程序集是否受影响。

涉及程序集结构的修改应完成 Unity 编译验证。

## 九、Play Mode 操作

进入 Play Mode 前必须：

- 确认是否需要保存当前任务产生的 Scene 或 Prefab 修改。
- 检查是否存在用户未保存内容。
- 确认进入 Play Mode 是验证所必需。
- 记录验证前的重要状态。

Play Mode 中必须注意：

- Play Mode 修改通常不会持久化，不得把运行时临时状态当作资产修改结果。
- 不得为了保留运行时修改而绕过正常的 Scene 或 Prefab 编辑流程。
- 不得在用户正在手动测试时擅自停止 Play Mode。
- 异步、事件、对象池和场景切换相关验证应检查生命周期边界。
- 需要截图时，应确保画面对应正确场景、正确状态和正确目标实例。

退出 Play Mode 后必须：

- 确认是否恢复到预期 Edit Mode 状态。
- 检查是否产生新的 Console 错误。
- 丢弃当前任务生成的临时运行时状态。
- 不得丢弃或覆盖用户已有的编辑器修改。

## 十、Console 和日志检查

检查 Unity Console 时必须区分：

- 操作前已经存在的历史日志。
- 本轮操作新产生的日志。
- 编译错误。
- 资源导入错误。
- 运行时异常。
- 与当前任务无关的第三方插件日志。

推荐流程：

1. 操作前记录当前 Console 基线或日志时间点。
2. 执行本轮操作。
3. 查询本轮新增日志。
4. 根据日志时间、类型和触发路径判断是否与本次修改相关。
5. 对相关错误继续定位，不把所有历史错误归因于本次修改。

不得为了得到“无错误”结果而擅自清空用户 Console。

如果确实需要清空 Console 才能隔离本轮日志，应先说明目的并获得用户同意，或者使用不影响用户历史信息的增量查询能力。

新增日志应包含有价值的上下文，例如：

- Scene、Prefab 或资源路径。
- GameObject 或组件名称。
- 配置 ID。
- 当前状态。
- 目标实例。
- 失败原因。

不得使用大量噪音日志掩盖流程问题。

## 十一、视觉内容验证

涉及以下内容时应进行视觉验证：

- Scene 布局。
- UI 布局。
- Prefab 外观。
- Material 和 Shader。
- 灯光、后处理和特效。
- 摄像机取景。
- 动画和 Timeline。
- 分辨率适配。
- 游戏运行画面。

视觉验证要求：

- 按有意义的修改批次截图。
- 截图前确认目标实例、场景、摄像机和运行状态。
- 截图应能清楚展示被修改内容。
- 必要时分别验证 Edit Mode、Game View 或 Play Mode。
- 多分辨率或多状态验证应覆盖需求明确要求的关键情况。
- 不重复生成没有新信息的截图。
- 发现遮挡、溢出、拉伸、丢材质、Missing Reference 等问题时，应回到资产或绑定层修复。

视觉验证不能替代以下检查：

- 序列化引用检查。
- Console 错误检查。
- 编译检查。
- 生命周期和交互流程验证。

## 十二、修改后验证

修改完成后，应根据改动类型选择必要验证，不默认执行全量测试。

### 1. 同时包含 C# 代码修改

根据风险执行：

- Unity 刷新。
- 等待脚本编译完成。
- 检查本轮新增编译错误。
- 检查受影响调用路径。
- 必要时进入 Play Mode 验证。
- 涉及公共接口时检查调用方。
- 涉及序列化字段时检查 Scene、Prefab 和 ScriptableObject 绑定。

### 2. Scene 或 Prefab 修改

至少检查：

- 目标对象和组件存在。
- 层级和 Transform 正确。
- 序列化字段值正确。
- 资源引用有效。
- 没有 Missing Script。
- 没有非预期 Override。
- 本次修改已保存到正确目标。
- 没有保存无关 Scene 或 Prefab 修改。

### 3. Material、Shader 和视觉资源修改

至少检查：

- Material 引用正确。
- Shader 编译正常。
- 关键参数正确。
- Scene 或 Prefab 中实际显示符合预期。
- 没有材质丢失、粉色错误材质或平台不兼容错误。
- 必要时通过截图验证。

### 4. 外部资源导入

至少检查：

- Unity 已完成导入。
- 资源类型正确。
- Importer 关键配置正确。
- 子资源数量和结构符合预期。
- GUID 没有冲突。
- 现有引用没有断裂。
- Console 没有新增导入错误。

### 5. 工程设置修改

至少检查：

- 设置写入正确目标项目。
- 修改值符合需求。
- 没有覆盖无关平台设置。
- 必要时重新读取设置确认结果。
- 涉及构建目标或 Player Settings 时说明对构建和版本控制的影响。

### 6. 验证范围原则

- 验证应与修改风险和影响范围匹配。
- 非必要不运行全量测试。
- 非必要不执行完整 Player 构建。
- 非必要不触发全项目重新导入。
- 小范围纯文本修改可以采用轻量检查。
- 修改 Unity 序列化内容、资源引用、生命周期或跨模块接口时，不能只做文本静态检查。
- 无法执行必要验证时，应明确说明未验证内容和影响范围。

## 十三、保存与收尾

### 1. 保存规则

- 只保存本次任务明确产生且有意义的修改。
- Scene 修改保存到正确 Scene。
- Prefab 修改保存到正确 Prefab 或正确的 Prefab Stage。
- ScriptableObject、Material 等资产应正确设置脏标记并保存。
- 不得使用“保存全部”覆盖无法确认归属的用户修改。
- 不得擅自保存与当前任务无关的 Scene、Prefab 或资源。
- 不得擅自丢弃用户已有未保存内容。

### 2. 临时测试内容

任务结束前必须处理本次任务产生的临时内容：

- 删除临时 GameObject。
- 移除临时组件。
- 恢复临时参数。
- 退出仅用于验证的 Play Mode。
- 丢弃运行时临时修改。
- 移除临时测试资源和临时编辑器脚本。
- 保留用户要求持久化的内容。

删除 `Assets/` 下的临时资源时仍必须使用 Pipeline。

### 3. 最终状态确认

完成 Unity 操作后应确认：

- 目标实例仍然明确。
- Pipeline 命令已经结束，不处于未知状态。
- Unity 不再处于非预期编译、导入或构建状态。
- Scene 和 Prefab 保存状态符合预期。
- Play Mode 状态符合预期。
- 本轮新增的相关 Console 错误已经处理或明确说明。
- 临时测试内容已经清理。
- 用户已有未保存内容没有被擅自保存或丢弃。

## 十四、异常处理

Pipeline 命令失败时不得盲目重复执行。

应先检查：

- 目标实例是否仍然存在。
- 是否连接到了正确项目。
- 命令是否被当前实例支持。
- Unity 是否正在编译、导入、切换模式或弹出对话框。
- 目标 Scene、Prefab、资源或对象是否仍然有效。
- 前一步操作是否实际成功但响应超时。
- 是否存在部分成功状态。

对非幂等操作，例如创建、复制、移动、删除或添加组件，在重试前必须重新查询目标状态，防止重复创建或重复修改。

失败信息应包含：

- 操作名称。
- 目标实例。
- 目标对象或资源路径。
- 使用的关键参数。
- 当前 Unity 状态。
- Pipeline 返回的错误。
- 已经完成和未完成的部分。

不得通过以下方式掩盖失败：

- 吞掉异常。
- 添加无业务语义的判空后静默跳过。
- 改用硬编码路径。
- 运行时补组件。
- 直接修改序列化文件。
- 不验证结果却报告成功。

## 十五、最终回复要求

完成 Unity 相关任务后，最终回复应简洁说明：

- 修改了哪些脚本、资源、Scene、Prefab 或工程设置。
- 哪些内容通过普通文件编辑完成。
- 哪些内容通过 Unity Pipeline 完成。
- 使用了哪个 Unity 目标实例；只有一个实例且不存在误操作风险时可简化说明。
- 为什么采用当前操作方式。
- 影响范围是什么。
- 是否检查了同类代码、资源或引用。
- 实际执行了哪些刷新、编译、Play Mode、截图、日志或其他验证。
- Unity Console 是否出现本轮新增的相关错误。
- 是否保存了 Scene、Prefab 或其他资产。
- 是否清理了临时测试内容。
- 哪些验证没有执行以及原因。
- 只有存在真实不确定性时，才说明残余风险。

不得把以下状态描述为“已经完成并验证”：

- Pipeline 不可达。
- Unity 编译状态未知。
- 资源导入尚未结束。
- 目标实例不明确。
- Scene 或 Prefab 未正确保存。
- 只修改了文本但没有完成需求所需的 Unity 侧验证。
- Console 中存在尚未判断的本轮新增错误。

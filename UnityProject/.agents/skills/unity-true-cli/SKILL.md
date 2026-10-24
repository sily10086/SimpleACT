---
name: unity-true-cli
description: 用官方 Unity CLI 操作 Unity Editor，制作和检查场景、镜头、UI/TMP、输入及渲染效果；提供按任务发现、批量配方、结果恢复和调用耗时审计。适用于实际 Unity 项目制作与自动操作，当前发行版为 Windows x64。
---

# Unity True CLI

此包包含固定版本官方 CLI 原件 与基于官方 Pipeline 源码的兼容包
使用本 Skill 的脚本入口 不依赖全局 unity 或其他 Unity 自动化工具
先读 [兼容矩阵](references/compatibility.json) 区分候选系列与实际验证版本
发行包 0.8.0，Pipeline 0.8.0-exp.1+truecli.9。当前发行验证以兼容矩阵 current_release_verification 为准；historical_verification 仅保留旧版本证据，不代表本版在所有补丁通过。
完整 [命令索引](references/command-index.md) 和 [机器索引](references/command-index.json) 覆盖 277 个名称、310 处实现；Cinemachine 2/3 共用 33 个名称，两个捕获命令需 Unity 6000.7+。索引用于找能力，实际可用性与参数以当前工程实时 schema 为准。

## AI 从这里开始

先确认目标工程与用户要改变的结果。按任务查询当前服务，再取选中命令的 schema；不需要每轮输出整个目录。

```powershell
& '<Skill目录>/scripts/Start-UnityTask.ps1' -ProjectPath '<Unity工程>' -Task camera -Query '第三人称'
& '<Skill目录>/scripts/Start-UnityTask.ps1' -ProjectPath '<Unity工程>' -Command configure_cinemachine_third_person
```

任务支持 `camera/ui/tmp/input/rendering/ssu/observe` 及中文别名；省略 Task 返回任务入口，`-Query` 单独使用可搜索核心命令。返回的是当前注册项；缺少可选包时不出现，不能据静态列表假定已安装。`-DiscoverOnly` 只连接现有就绪服务；默认准备可能安装 Pipeline 或启动精确版本 Editor。

制作多步对象树时用原生 `batch`，跨帧条件用 `wait_for`，长任务用原生 `--detach` 和 `job`。可直接改用 [两份可执行配方和恢复说明](references/ai-workflow.md)；配方一次准备、保存原 job/wait ID、逐步核内层结果，恢复时跳过已确认写入并重新读取当前对象。Cinemachine 2/3 每代已有 33 个命令，优先用专用接口；业务逻辑仍可用 eval，不为减少 eval 数量强行替换。

## 单命令调用

脚本路径相对本 Skill 目录 项目路径必须直接包含 ProjectSettings/ProjectVersion.txt

```powershell
& '<Skill目录>/scripts/Invoke-Unity.ps1' -ProjectPath '<Unity工程>' -UnityArgs @('command', '--tags', '--json')
& '<Skill目录>/scripts/Invoke-Unity.ps1' -ProjectPath '<Unity工程>' -UnityArgs @('command', 'editor_status', '--json')
```

首次 Editor 操作自动校验整包 识别工程精确 Editor 版本 并检测已有 Pipeline 服务
已有服务就绪时直接使用 否则把兼容包复制到工程 Tools/UnityTrueCLI/com.unity.pipeline
通过临时安装请求和 Assets/Editor/UnityTrueCli/PackageBootstrap.cs 调用 Unity PackageManager.Client.Add
Pipeline 依赖由 UPM 正常解析 首次安装可能访问 Unity 包注册表
不直接修改 manifest 不编辑 Library/PackageCache 不重复安装就绪包
Editor 已打开时复用该进程 未打开时以 batchmode/nographics 启动精确 Editor 并保留供后续命令使用
需要图形视图时在已有交互式 Editor 中执行 冷启动的后台实例不提供图形捕获
安装过程会导入脚本并可能触发程序集重载 必须在用户授权的目标工程操作
没有精确 Editor 时报告缺失 不下载 Editor 或改用相邻补丁

仅安装并在冷启动后退出 Editor

```powershell
& '<Skill目录>/scripts/Invoke-Unity.ps1' -ProjectPath '<Unity工程>' -InstallOnly
```

自动识别当前工程进程、UNITY_EDITOR_PATH、Unity Hub 安装元数据和默认目录；D 盘等 Hub 自定义安装路径也支持。找不到时使用 -EditorPath '<精确版本 Unity.exe>'，仍会核对文件版本。
全局管理命令直接透传 例如 -UnityArgs @('editors', '--installed', '--json')
Editor 操作统一走 command 不透传原生 open close run build test recompile 快捷入口
需要编译或测试时先查当前 Pipeline 的 recompile 与 run_tests 命令

## 操作

先发现当前服务的命令与参数 再执行操作 不假定所有版本提供相同命令
每次机器调用带 --json command 的 caller 与 skill 标签由入口添加
场景和资产通过命令或 eval 使用真实 Unity API 不手写 Unity 序列化文件
UI/TMP、Input System、Cinemachine 和持续画面操作先查 get_cli_extensions，再读 [CLI 扩展](references/cli-extensions.md)；所有能力统一走 command，缺少可选包时对应命令不会出现
用户授权连续观察时，在当前工作回合持续执行取帧、实际打开图像、分析与后续操作循环；只启动采集或只读图片路径不能称为持续分析
eval 与 eval_file 默认缓存成功编译的相同源码 每次仍执行 需要新的程序集与静态状态时传 --cache false
多步关联对象操作优先查 batch 的当前 schema 不把任意 eval 当成可回滚事务
异步操作保留 job ID 查询原结果 请求结果未知时不自动重发写命令
普通文件写入不等于 Unity 导入或编译成功 按任务回读并核实最终状态
保护现有未保存场景 不自动保存 关闭 Editor 停止 Play 或运行测试
安装失败保留 Temp/UnityTrueCLI 的原请求 结果和 Editor 日志 不通过删除请求来重试
当前安装中请求目录丢失时沿原 ID 和原截止时间恢复通信 不另发一次安装 已知旧版工具 bootstrap 更新会先备份并保留 meta 未知用户修改仍拒绝
只有已取得同 ID 失败结果并修复原因时才显式使用 -RetryFailedInstall 旧请求会归档保留

## 扩展阅读

- [SSU 材质与效果](references/ssu.md) 提供本地授权资产安装、11 个正式命令、Image/RawImage/Shape 原创尺寸与遮罩材质桥，以及已知 URP 17 2D Shader 的显式本地修补

- [URP 视觉制作](references/visual-authoring.md) 涵盖原生斜边／切角 UI、镜头与 Timeline 接线、typed Shader globals、Volume 与官方 Full Screen Pass；按实际包发现入口并验证真实效果
- [UI 搭建与 3D 面板](references/ui-authoring.md) 提供 Canvas/RectTransform、完整基础按钮树和原生布局制作；先查当前 schema，再组合创建、编辑、查询与真实交互
- [Cinemachine 33 个命令](references/cinemachine.md) 用于两代管线、第三人称/构图/环绕、多目标、轨道、避障、噪声/冲击和镜头管理；未逐项验收的高级组合不得说成全部通过
- [CLI 扩展](references/cli-extensions.md) 涵盖 UI/TMP、Input Actions、跨帧输入、Cinemachine 2/3 和持续观察命令，以及旧工程显式升级
- [UI 布局与排序查询](references/ui-inspection.md) 用于读取 RectTransform、Canvas、父级缩放与 CanvasGroup 当前事实；尚不包含遮挡判定或点击命中检查
- [源码与协议](references/implementation.md) 用于学习实现或添加命令
- [来源与兼容改动](references/provenance.json) 用于核对官方版本与修改范围
- 原始许可证与第三方声明位于 assets/com.unity.pipeline 不代表官方认证此兼容发行包

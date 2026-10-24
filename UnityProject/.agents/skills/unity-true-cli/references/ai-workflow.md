# AI 制作入口与耗时诊断

`Start-UnityTask.ps1` 做一次整包校验、精确 Editor 匹配、服务准备和任务发现；`-PrepareOnly` 返回已验证的 CLI 路径、版本及服务指纹，供常驻制作脚本接入。项目、Editor 进程生命、服务或工具变更后重新准备；它不是永久连接票据。

## 两份原生配方

路径相对 Skill 目录。输入 JSON 为 UTF-8，状态目录每次新建；同一轮续办使用原目录和 `-Resume`。

```powershell
& '<Skill目录>/scripts/Invoke-UnityRecipe.ps1' -ProjectPath '<工程>' -RecipePath '<Skill目录>/assets/recipes/cinemachine-camera.json' -StateDirectory '<新状态目录>' -ValidateOnly
# 用另一个新目录实际创建：
& '<Skill目录>/scripts/Invoke-UnityRecipe.ps1' -ProjectPath '<工程>' -RecipePath '<Skill目录>/assets/recipes/cinemachine-camera.json' -StateDirectory '<执行状态目录>'
# 续办同一次运行，已确认写入跳过，但 inspect 重新执行：
& '<Skill目录>/scripts/Invoke-UnityRecipe.ps1' -ProjectPath '<工程>' -RecipePath '<Skill目录>/assets/recipes/cinemachine-camera.json' -StateDirectory '<执行状态目录>' -Resume
```

镜头配方创建独立根、Follow/LookAt、第三人称管线、构图和 48° FOV；优先级 -100，是否成为 live 镜头取决于当前 Brain 和其他镜头，配方不证明已拍到画面。UI 配方创建切角渐变面板和中文 TMP 标题，需 `-InputsPath '<输入.json>'`，例如：

```json
{"root":"AI_状态面板","text":"系统就绪","font":{"path":"Assets/Fonts/Chinese-SDF.asset"},"fontPath":"Assets/Fonts/Chinese-SDF.asset"}
```

字体必须是工程实际 TMP FontAsset，并包含所需汉字；示例不分发字体。 寻找现成字体可先取 `find_assets` 的当前 schema，再用 `--type TMPro.TMP_FontAsset --limit 20` 查询；返回的 assetPath 可直接作为输入。对象树的真实命令是 `get_scene_hierarchy`。不要用其它工具的旧名称猜当前入口。新根名称若存在则前检拒绝；换新状态目录不代表允许重复创建。回读对象结构和字体不等于视觉/命中验收，按实际任务继续截图或输入验证。配方不会保存场景或关闭 Editor。

镜头示例的 `cameraType` 默认 `Unity.Cinemachine.CinemachineCamera`（CM3）。CM2 可按当前组件 schema 显式输入 `Cinemachine.CinemachineVirtualCamera`；本批整份配方实测为 CM3，不把参数可改等同 CM2 已复验。镜片回读使用只含 inspector 的原生 batch；自定义 verify/preflight 的 batch 也只允许已列出的只读检查命令。

`-ValidateOnly` 验证结构并执行原生 batch dry-run，未创建对象，也未证明资产引用、最终几何或视觉成立。自定义配方用 `steps` 写入、`verify` 当前只读检查，可选 `preflight` 只读前检；各步 `assert` 支持 `equals/contains/exists` 及点路径/数组索引。不提供通用脚本语言，跨操作引用继续用原生 `$id.field`。

## 正确处理结果与恢复

不能仅看进程退出码、outer.success 或 job completed。还要看 eval 内层 success、batch 每项结果/回滚/跳过、wait 条件是否 met 以及 onMetError。先保存句柄与返回，再断言；断言未完成不能标记完成。

状态目录保存完整本地回执、输入哈希和步骤状态。未知结果且没有原 job/wait ID 时停止自动提交，查询原对象或人工核实；客户端超时不会证明 Editor 未执行。原生 job/wait 注册表在内存中，域重载或过期后 not_found 不证明没有副作用。取消也不能据此重发。服务生命改变后旧状态拒绝自动恢复，准备新轮并核原消费者。

配方按步骤串行完成，`wait_for` 即使 async 也会等原 wait 达成后才运行下一步，因此只适合 Editor 自行推进的条件。先监听、后触发、最后接回的操作须在配方外使用原生 async wait/后续动作/wait_status；不能把依赖下一步才达成的 wait 放在配方前面。

`-TracePath '<timing.jsonl>'` 可用于单命令、任务和配方入口，只记阶段/命令名/时长/状态/退出码/字节数，不记参数和输出。完整本地回执可能含项目数据，分享前自行裁剪。计时写失败只提示，不覆盖命令原结果。

## 扫描历史耗时

```powershell
python '<Skill目录>/scripts/analyze_ai_workflow.py' --sessions-dir '<Codex sessions目录>' --root-thread '<目标会话UUID>' --evidence-dir '<CLI回执目录>' --output '<新报告.json>'
```

只扫描指定根的真实 parent_thread_id 后代与明确提供的回执目录；也可重复 `--rollout` / `--evidence-dir`。流式处理并输出聚合数据，不导出对话、命令参数、源码、环境或密钥。工具并行耗时取时间并集，未归因间隔不能称为纯模型思考；回执文件名到mtime仅是墙钟代理，不能直接当可省成本。源码命令分类不证明历史服务已注册，eval 词法命中仅供人工调查。`status=partial` / 退出码2 表示源有缺口，应连同 source_errors 等边界一起读。报告拒绝覆盖已有文件。

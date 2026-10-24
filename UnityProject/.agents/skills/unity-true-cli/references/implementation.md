# 实现与来源

CLI 固定为官方 1.0.0-beta.13 Windows x64 原件 SHA256 由官方 CDN 对应版本清单核对
本机已有同版本程序与 CDN 哈希不同 没有把该程序作为发行原件
CLI 的 PE 没有 CLR 元数据目录 可观察到 NativeAOT 运行时字符串
因此没有声称恢复完整可编译 CLI 源码 当前整包保留原版客户端
协议研究结合程序帮助 实际命令往返与配套官方 C# 源码完成

Pipeline 源自官方 com.unity.pipeline 0.8.0-exp.1
保留命令服务器 主线程调度 命令发现 Roslyn 编译 CodeGen 和 IL 解释器源码
兼容发行号为 0.8.0-exp.1+truecli.5 原始版权与许可证完整保留

## 通信

Editor 将进程 项目 端口 能力与会话 Token 写入 Library/Pipeline/.unity-pipeline-port
客户端读取该文件 只连接 127.0.0.1 并在 Authorization Bearer 头中携带 Token
Token 不进入工具输出 文档或共享产物

- GET /api/status 提供服务状态
- GET /api/commands 提供命令与参数 schema
- POST /api/exec 接受结构化 command/parameters 或原始 argv/commandLine
- job/progress 端点提供异步任务与进度查询

CommandRegistry 通过 TypeCache 发现 CliCommandAttribute 并支持动态注册
BasePipelineServer 执行参数转换与校验 按 MainThreadRequired 选择 Dispatcher 或后台线程
Dispatcher 通过 EditorApplication.update 处理队列
eval 使用包内 Roslyn 编译 C# 后在 Unity 进程执行
相同源码默认复用成功编译的程序集 每次仍执行代码并重新序列化结果 不缓存编译失败或执行结果
eval 与 eval_file 共用 128 项 LRU 缓存 引用实例集合或项目编译符号变化时清空 域重载后重建
LRU 只限制缓存条目 Mono 已加载程序集在域重载前不会因淘汰而卸载
复用会保留生成程序集的类型身份和潜在静态状态 需要每次全新编译时传 --cache false
引用发现沿用上游按 DLL 路径缓存元数据的实现 不保证识别已加载 DLL 的原地替换
入口每次进行一次完整文件 SHA256 校验 安装逻辑复用同次校验得到的上下文 不省略就绪服务探测
代码重载与 IL 解释器的支持范围以具体平台和命令实际响应为准

0.2.0 的 UI/uGUI、TMP、Input System 和 Cinemachine 2/3 通过独立 Editor 适配程序集接入原 CommandRegistry。返回数据在主线程生成完整快照，避免 HTTP 序列化阶段访问 Unity API。持续观察使用 Editor update 采集、有界图片 ring 和不可变状态文件发布；输入序列使用自有虚拟设备与正常 Input System 更新，不重入 Update。
新会话/输入命令排除在同步 batch 事务外，Input Actions 的持久写入不能冒充可 Undo 的事务。具体参数与限制见 [CLI 扩展](cli-extensions.md)。

0.3.0 为 Cinemachine 两代增加同名高级入口，完整范围见 [Cinemachine](cinemachine.md)。管线安装/移除采用对应代际的实际组件机制，并保留 Undo；通用组件配置桥复用已有 SerializedObject 转换器，暴露实际嵌套路径和类型，阻止修改对象身份。目录通过 Unity TypeCache 查询已安装官方包类型，不猜不存在的成员。
Windows 上 Input Actions 持久写入改为同目录临时文件加原生原子重命名，避免 Mono File.Replace 在回归中出现的替换失败；暂时文件锁只做有界发布重试，不截断或删除原资产。

## 2022.3 移植

- 新式分析事件通过适配器映射到 RegisterEventWithLimit 和 SendEventWithLimit
- Console 计数使用旧 Editor 的 LogEntries.GetCountsByType 保留定时采样
- PhysicsMaterial 使用旧版 PhysicMaterial 类型
- 材质继承渲染队列通过 SerializedObject 读取原始值
- 编译事件使用明确的 UnityEditor.Compilation.CompilationPipeline 避免命名空间冲突
- DLL 导入设置由真实 2022.3 PluginImporter 导出为旧版可识别的格式 保持原始 GUID 与程序集
- 安装器通过真实 PackageManager.Client.Add 异步安装 并等待程序集与 HTTP 服务就绪

兼容源码独立于项目业务 不含其他自动化工具的传输或序列化实现
静态 API 编译不等于 Unity 编译 HTTP 服务就绪不等于所有功能均已验证

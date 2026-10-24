# Cinemachine CLI：当前 33 个命令

本版为 Cinemachine 2 与 3 各提供 33 个相同名称的 CLI 入口，使用各代真实类型与字段。先运行 `get_cinemachine_catalog` 和当前命令 schema，再使用对象句柄。包版本以实际输出为准；不将“存在通用属性入口”解释为所有镜头效果都已逐项验收。

| 范围 | 命令 |
| --- | --- |
| 基本状态与相机 | inspect_cinemachine、create_cinemachine_camera、configure_cinemachine_camera |
| Brain 与实时切镜 | inspect_camera_blend、configure_cinemachine_brain、prioritize_cinemachine_camera |
| 真实组件目录与管线 | get_cinemachine_catalog、inspect_cinemachine_pipeline、set_cinemachine_pipeline、add_cinemachine_extension、remove_cinemachine_component |
| 组件全量属性路径 | inspect_cinemachine_component、configure_cinemachine_component |
| 镜头、构图与第三人称 | configure_cinemachine_lens、configure_cinemachine_composition、configure_cinemachine_third_person |
| 自由环绕 | create_cinemachine_freelook、configure_cinemachine_orbit |
| 多目标 | create_cinemachine_target_group、set_cinemachine_group_members |
| 边界和避障 | configure_cinemachine_confiner、configure_cinemachine_obstacles |
| 持续噪声与冲击 | create_cinemachine_noise_profile、configure_cinemachine_noise、create_cinemachine_impulse_source、emit_cinemachine_impulse |
| 轨道 | create_cinemachine_path、configure_cinemachine_dolly |
| 镜头管理 | create_cinemachine_manager、set_cinemachine_sequence、set_cinemachine_mixer_weights、set_cinemachine_state_rules |
| 自定义混合 | set_cinemachine_custom_blends |

## 选择管线后配置

`set_cinemachine_pipeline` 的 Body：follow、framing、third_person、orbital、dolly、hard_lock；Aim：composer、pov、hard_look、rotate_with_target；Noise：perlin。选择不属于该阶段的算法会拒绝。替换已有不同算法必须显式 `replace=true`。替换、配置和场景对象创建支持 Undo。

例如第三人称：

```text
command set_cinemachine_pipeline --target '{"instanceId":123}' --body third_person --aim composer --replace true --json
command configure_cinemachine_third_person --target '{"instanceId":123}' --distance 7 --shoulder '[0.5,1,0]' --side 0.25 --json
command configure_cinemachine_composition --target '{"instanceId":123}' --screen '[0.6,0.4]' --dead_zone '[0.1,0.15]' --json
```

构图 screen 统一用 0..1；3.x 的中心坐标显式转换。查询真实字段时仍保留该代原有名称。帧构图的更多距离/阻尼/组取景参数，可通过组件的实际序列化路径设置；不要把 CM2 m_ 字段名直接套到 CM3。

`inspect_cinemachine_pipeline` 会返回管线组件自身句柄，2.x 的隐藏 cm 子对象也会进入结果。用这个句柄调用 `inspect_cinemachine_component`，可获得最多 2048 个实际属性路径及类型，含嵌套字段和隐藏的序列化配置。`configure_cinemachine_component` 复用 Unity SerializedObject 和既有 Undo 写入，不修改脚本/GameObject/Prefab 身份。曲线等既有转换器不支持的字段会明确报错，不宣称可写。

## 环绕、多目标和轨道

2.x 的 FreeLook 使用原生三套 rig；3.x 使用 CinemachineCamera + OrbitalFollow 三环 + RotationComposer。CLI 可直接设置水平角、归一化竖直轴，以及上/中/下环的高度和半径。创建时不偷偷接入旧 Mouse X/Y 或修改输入后端；实时用户控制需要显式连接工程选择的 Input Provider/Axis Controller。

目标组成员为 `{target:ObjectRef,weight:1,radius:0.5}`；替换前先解析全部身份，拒绝重复对象及非法权重/半径。将组对象设置为相机的 Follow/LookAt，再选择 framing 管线；3.x 还可显式安装 group_framing 扩展。2.x 对应组构图来自 FramingTransposer/GroupComposer，不假造一个同名扩展。

`create_cinemachine_path` 根据两代分别创建 CinemachineSmoothPath 或 SplineContainer。`configure_cinemachine_dolly` 使用归一化 0..1 位置。轨道点属于新对象本地坐标，支持闭环；不自动把已有相机切成轨道管线。

## 避障、边界与震动

先 `add_cinemachine_extension`，再配置具体能力：

- collider：2.x Collider；3.x Deoccluder。
- confiner2d / confiner3d：分别绑定真实 Collider2D/Collider 并使对应缓存失效。
- impulse_listener、camera_offset；3.x 另有 decollider 和 group_framing。

2D 边界是否是当前版本支持的形状、3D Collider 几何及碰撞层是否正确，仍需要实际运行验证。配置命令不会把指定碰撞体自动改成另一个形状。Physics、Physics2D 和 Animation 引擎模块是本版明确依赖，避免这些官方类型因条件编译缺失。

持续噪声需 perlin 管线和 NoiseSettings 资源。创建 NoiseSettings 会写新资产，不覆盖已有文件；设置 gain/profile 支持 Undo，显式 reseed 会改变运行相位。

冲击源默认使用非 Legacy 的 Uniform impulse，明确时长和通道；接收镜头需要匹配的 listener。`emit_cinemachine_impulse` 只在 Play Mode 发出一次真实事件；结果未知时不能自动重发。实际画面响应应结合 Brain、观察帧和 listener 配置回读，事件已发出不代表所有相机均收到。

## 镜头管理与混合

Manager 类型支持 clear_shot、mixing、state_driven、sequence。创建 Manager 不自动收编现有相机，使用既有父子关系命令显式设置。序列规则和 Animator 状态规则只接受 Manager 的直接子镜头。

- Sequence：每项 camera、hold、blend；支持循环。2.x 对应 BlendListCamera，3.x 对应 SequencerCamera。
- Mixing：每个直接子镜头一个非负权重，最多 8 个，至少一个正值。
- State-driven：显式 Animator、layer、full_path hash、camera、activate_after、min_duration。命令不猜状态名称，也不把未查证的 hash 说成已匹配 Animator 状态。
- Custom blends：新建 BlenderSettings 资产，明确 from/to 名称、duration/style，随后绑定 Brain。原资产保留，资产创建不可 Undo；Brain 引用变更可 Undo。
- Prioritize：只调整同数值优先级的队列次序，不擅自提高数值优先级，也不保证混合瞬间结束。

这些是 Editor 制作及 Play Mode CLI 入口。Timeline 轨道绑定、后处理资源及物理镜头的所有组合尚无逐项专用验收；可以使用实际组件/通用序列化 API 继续制作，但不写成已覆盖全部 Cinemachine 功能。

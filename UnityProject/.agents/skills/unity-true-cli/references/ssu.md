# Sprite Shaders Ultimate CLI

0.6.0 内置 11 个正式 CLI 命令与原创 uGUI 材质桥。SSU 由目标项目自行安装；本包不含供应商 Shader、纹理或脚本。缺少 SSU 时 core 仍能编译，`get_ssu_catalog` 返回 `installed=false`。

## 安装与发现

已有 SSU 的工程直接查询。需要把自己已获许可的本地副本装入另一个工程时，先预检，再复制：

```powershell
& '<Skill>/scripts/Install-SSU.ps1' -SourcePath '<已有SSU目录>' -ProjectPath '<目标工程>' -DryRun
& '<Skill>/scripts/Install-SSU.ps1' -SourcePath '<已有SSU目录>' -ProjectPath '<目标工程>'
& '<Skill>/scripts/Invoke-Unity.ps1' -ProjectPath '<目标工程>' -UnityArgs @('command','get_ssu_catalog','--json')
```

安装器只复制 Shaders/Textures/Scripts 及完整 meta，保留 GUID；不带 Demo/ASE。目标目录已存在、文件缺 meta、GUID 与项目/包冲突、源目标嵌套或路径包含 junction 时拒绝。文件复制完成返回 `copied_pending_editor_import`；需要实际 Editor 导入和编译后才能判断可用。

当前本地输入发现 10 种 Shader，GUI 有 66 个效果开关。版本用源指纹描述，不能从 ASE 版本推断 SSU 版本；其它 SSU 版本按实际 catalog 为准。同名 Shader 有多个资产时名称查询会拒绝歧义，请改传 `--shader_ref '{"globalId":"<catalog中的Shader ID>"}'`。

## 命令

| 命令 | 用途 |
| --- | --- |
| `get_ssu_catalog` | Shader 变体、真实属性/类型/范围/attributes/keywordSpace；可传 shader/filter |
| `create_ssu_material` | 新建指定 Shader 的材质，不覆盖 |
| `configure_ssu_material` | typed 属性与 Toggle/KeywordEnum 同步；完整预校验后保存 |
| `configure_ssu_effects` | 按实际效果名批量开关和配置本组参数 |
| `inspect_ssu_material` | 属性值、开关 float、实际 keyword、mismatch、资产身份 |
| `assign_ssu_renderer_material` | 精确 Renderer/SpriteRenderer 的 sharedMaterials slot 分配/清除，支持 Undo |
| `inspect_ssu_renderer` | 材质槽位、SSU 状态、PropertyBlock 是否存在，不隐式实例化 |
| `assign_ssu_ui_material` | 给 Graphic 分配 GUI SSU，可启用实际 Rect/图集桥，支持 Undo |
| `configure_ssu_ui_bridge` | 开关桥、实际 Rect 和 Image 矩形图集同步 |
| `inspect_ssu_ui` | 直接引用、已提交材质、桥 owned/borrowed 实例与尺寸/遮罩状态分别回读 |
| `set_ssu_ui_parameters` | Play Mode 修改桥实例参数，重建时保留临时覆盖，支持 reset |

使用现有 `command` 发现 schema；UI 四个命令需要 uGUI。所有资产编辑只在 Edit Mode 执行，Renderer/Graphic 目标可以是精确组件 ObjectRef；同对象有多个候选组件时必须给出精确身份。

## 创建并启用效果

以下参数放入 `Invoke-Unity.ps1 -UnityArgs`，每次命令加 `--json`。材质目录须已存在：

```text
command get_ssu_catalog --shader "Sprite Shaders Ultimate/GUI SSU" --filter Hologram --json
command create_ssu_material --path Assets/Hud.mat --shader "Sprite Shaders Ultimate/GUI SSU" --properties '[{"name":"_ShaderSpace","choice":"UI_Graphic"}]' --json
command configure_ssu_effects --material '{"path":"Assets/Hud.mat"}' --effects '[{"effect":"StrongTint","enabled":true,"parameters":[{"name":"_StrongTintTint","color":[0.15,0.8,1,1]}]}]' --json
command assign_ssu_ui_material --target '{"hierarchyPath":"/Canvas/Panel"}' --material '{"path":"Assets/Hud.mat"}' --json
```

`effect` 接受 catalog 中的 `_EnableStrongTint` 或其后缀 `StrongTint`。效果参数必须属于该声明组，错误末项会让整次请求在修改前失败。`configure_ssu_material` 的属性项恰好提供一种值：`number`、`integer`、`color:[r,g,b,a]`、`vector:[x,y,z,w]`、`texture:ObjectRef`、`clear_texture:true` 或 `choice`。Toggle 根据真实声明同步 float 与 keyword；没有声明 keyword 的动态 bool 不会凭空造 keyword。`KeywordEnum` 会关闭同组旧选项。

材质编辑会保存资产并影响其共享消费者。资产写入及运行临时参数不属于 batch 的可回滚事务；场景材质分配和桥配置支持 Undo。需要独立外观时先创建另一份材质资产。

Play Mode 只修改预先启用效果的参数：

```text
command set_ssu_ui_parameters --target '{"hierarchyPath":"/Canvas/Panel"}' --properties '[{"name":"_StrongTintTint","color":[1,0.2,0.3,1]}]' --json
```

运行时不切 Toggle/KeywordEnum，以免请求被裁剪的 shader_feature 变体。桥不写源资产；临时覆盖会跨 Rect/遮罩重建重放，禁用桥时清除并释放本桥创建的实例。`--reset true --properties '[]'` 清除临时覆盖。

## UI 与管线选择

- Image、RawImage、原生 Shape 使用 GUI SSU / Additive GUI SSU。桥读取 `rectTransform.rect` 的实际尺寸，支持 Stretch 与父布局变化，不依赖可能为零的 sizeDelta。
- 桥在原生 Graphic/Mask 材质 modifier 后工作；继承实际 stencil/clip 状态，效果读取真实源材质，避免共享遮罩缓存阻挡更新。只销毁本桥创建的材质，不销 Unity 缓存或供应商实例。
- 同 Shader 的源参数更新复用已有桥材质；Shader 改变时才重建该实例。CanvasGroup 透明度与 RectMask2D 已有本批实际 GPU 对照。
- 已有供应商 Instancer/SpriteSheet/Fader 管理同一 Graphic 时拒绝叠加；Fader 的手动列表只影响其中目标。其它自定义材质 modifier 需要明确适配。
- `--sprite_sheet true` 仅支持 Image 可验证的非旋转矩形图集区域；tight/rotated packing 拒绝。RawImage/Shape 使用各自原生 UV。
- Standard/URP Lit 用于对应 Renderer。2D Lit 需要 URP 2D Renderer/Light2D，3D Lit 需要合适的 3D renderer/光照，不能替代 GUI 遮罩合同。
- Text/TMP 需要对应 SSU 文字选项与单独验证，不能从 Graphic 通用接口推导所有文字效果可用。Shader 的 GPU 位移、透明与 UV 变换不会自动改变 CPU 点击轮廓。
- Bloom 需要真实 HDR、Volume 与适合的 Camera/World Canvas；Overlay UI 不因开 Bloom 就自动发光。

## 当前本地 SSU 的 URP 17 2D 修补

已知本地 2D Lit Shader 的两处 `SETUP_DEBUG_DATA_2D` 仍用两参数，URP 17 改为三参数。可显式二开目标副本：

```powershell
& '<Skill>/scripts/Repair-SSUUrp2D.ps1' -ProjectPath '<目标工程>' -UrpPackagePath '<实际解析的com.unity.render-pipelines.universal目录>' -DryRun
& '<Skill>/scripts/Repair-SSUUrp2D.ps1' -ProjectPath '<目标工程>' -UrpPackagePath '<实际解析的com.unity.render-pipelines.universal目录>'
```

脚本核对实际 URP 17 package 与三参数宏，只接受已知原始 Shader SHA256；修补两处调用，原字节备份到目标 `Temp/UnityTrueCLI/ssu-patches`，保留 meta/GUID。未知版本拒绝，已修补返回事实。结果仍需 Editor reimport/ShaderUtil/GPU 确认。原供体不改，发行包只含补丁工具；此补丁针对 URP 17，返回 URP 14 需显式恢复原字节备份。ASE 重生成可能覆盖修补。不能使用 `SHADER_LIBRARY_VERSION_MAJOR>=17` 判断该差异：实测 URP 14/17 的这项宏均为 13。

验证以[兼容矩阵](compatibility.json)及[本次记录](verification-0.8.0.json)的范围为准；旧仓库 docs/verification/ssu-0.6.0.json 仅为历史材料，不随此包分发。本次按用户要求仅验新增修改项，完整 SSU 未重验通过。发现全部效果不等于所有效果组合均做过 GPU 验证；Player 和其它精确 Editor/SSU 版本尚无本批运行依据。

来源：[供应商文档](https://ekincantas.com/sprite-shaders-ultimate/)、[Asset Store 158988](https://assetstore.unity.com/packages/vfx/shaders/sprite-shaders-ultimate-158988)、[Unity Asset Store 条款 §2.2.1](https://unity.com/legal/as-terms)。本机项目 MIT 不替代供应商资产许可。

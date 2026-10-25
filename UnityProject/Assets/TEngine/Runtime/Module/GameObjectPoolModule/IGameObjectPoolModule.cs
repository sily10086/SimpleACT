namespace TEngine
{
    /// <summary>
    /// 游戏对象池模块的公共契约。
    /// <remarks>
    /// <para>
    /// <b>本接口只声明已经真正具备的能力。</b>借出、归还、预热、快照查询分别在第 05 至第 10 步实现，
    /// 到那一步才把对应成员加进来。这里不放"先占位、以后再实现"的成员——
    /// 那种成员对调用方是编译期通过、运行期永远失败或永远返回空的假 API，
    /// 比"没有这个成员"更难排查。
    /// </para>
    /// <para>
    /// <b>没有状态机</b>：模块没有"未初始化 / 就绪 / 正在关闭"这组对外状态。
    /// 它在 <c>OnInit</c> 里一次性读完配置并建好根节点，之后一直可用，直到框架关闭它；
    /// 配置缺失或内容有错就只打日志、不发布配置，此时各入口各自返回明确失败。
    /// 调用方要判断"能不能用"，看具体操作返回什么，而不是先问一个状态再去操作——
    /// 后者在两次调用之间仍然可能变化，该检查的还是要检查。
    /// </para>
    /// <para>
    /// <b>关闭不进本接口</b>：关闭是框架的模块生命周期（<c>Module.Shutdown</c>），
    /// 由 <c>ModuleSystem</c> 逆序关闭模块时调用，模块自己会在那里清理。
    /// 在本接口再声明一次只是把同一件事说两遍，还会诱使业务主动去关一个由框架管理的模块。
    /// </para>
    /// <para>
    /// 模块实现类型按框架约定必须叫 <c>TEngine.GameObjectPoolModule</c>：
    /// <c>ModuleSystem.GetModule</c> 用 <c>{命名空间}.{接口名去掉开头的 I}, {接口所在程序集}</c>
    /// 反射查找实现类型。因此本接口要留在 <c>TEngine</c> 命名空间，
    /// 且接口名只允许一个 <c>I</c> 前缀。
    /// </para>
    /// </remarks>
    /// </summary>
    public interface IGameObjectPoolModule
    {
        /// <summary>
        /// 已登记的池配置数量。
        /// <remarks>
        /// 与运行池数量是两回事：配置只是"打算有哪些池"，
        /// 建池由第 04 步的 TryGetPoolOrCreate 发起（同步），失败或未发起时这里照样计数。
        /// 配置没读成功（资产缺失或内容有错）时为 0。
        /// </remarks>
        /// </summary>
        int ConfigCount { get; }

        /// <summary>
        /// 追加一条池配置，只登记、不建池。
        /// <remarks>
        /// 不加载资源、不实例化、不创建任何池。
        /// 池名重复一律拒绝；池名与地址是否合法在编辑器下逐条校验，
        /// 发布版沿用同一份已经校验过的数据，不重复计算。
        /// </remarks>
        /// </summary>
        /// <param name="config">配置项。</param>
        /// <returns>登记是否成功。</returns>
        bool RegisterPool(GameObjectPoolConfig config);
    }
}

# .asset 笔画坐标量化

仍使用原有 ScriptableObject 分片与加载器，没有 .bytes 或新的收集规则。
TextDrawPoints 序列化两种互斥数据：quantizedPoints（short x/y），或 floatPoints（Vector2 兜底）。
量化步长 1/4096，最大单轴误差 1/8192。points 属性按需还原并缓存 Vector2 列表，绘制和碰撞不变。
旧 points 字段通过 FormerlySerializedAs 兼容。存档模型、关卡配置和笔画编号没有变化。

Unity 菜单：`Tools/关卡/量化现有笔画坐标（保留asset）`。
重复执行不会再次量化。以后从 graphics.txt 生成新字形时，构造器自动量化。
不满足范围或三角剖分数量/面积校验的笔画保留 float。
如需恢复编辑用的高精度采样点，请使用版本库中的迁移前 .asset 或原始 graphics.txt 重新生成。
新的 .asset 和 GameLogic 应一起构建发布；已有内置 Bundle 需要正常重打，才能体现体积变化。

在 TEngine 目录运行：

```powershell
dotnet run --project Tools/GraphicQuantizationTests -- UnityProject
```

检查所有分片结构、解码以及所有答案笔画索引；用生产存档类型和恢复类检查旧格式存档继续保存。
传入 `--migrate` 会对尚未迁移的 YAML 执行迁移，并检查误差和三角剖分数量，所有检查通过后写文件。
迁移后的源资源依旧是文本 YAML，文本大小不代表最终二进制 Bundle 大小。
测试使用生产量化/三角剖分类和 Unity 数学值操作桩；旧 JSON 由 System.Text.Json 读取。
这些检查不替代 Unity 播放模式的视觉、碰撞和 JsonUtility 恢复验证。

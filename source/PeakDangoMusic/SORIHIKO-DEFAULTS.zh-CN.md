# Sorihiko 原项目默认值对照 · PEAK 1.1.1

来源：[Sorihiko/DangoMusic 的 LyricsModule.kt](https://github.com/Sorihiko/DangoMusic/blob/da696d0871fa348323782d7d299ba10df46e07db/common/ui/src/main/kotlin/me/sorihiko/dango/module/impls/music/LyricsModule.kt)。固定提交：`da696d0871fa348323782d7d299ba10df46e07db`，读取日期2026-10-27。

以下为已适配的歌词选项。F9 → 歌词显示与位置 → “恢复 Sorihiko 原项目默认”可恢复这些值，并保留播放器来源、路径和端口。

| 原项目选项 | 原项目默认 | PEAK 对应 |
|---|---|---|
| 3D lyrics | true | 启用漂浮歌词=true |
| Scale | 1（0.5–3） | 缩放倍率=1；通过Unity字号换算，不直接把Minecraft像素单位当作TMP字号 |
| Distance | 10（3–40） | 歌词距离=10，使用从相机到文字的空间距离 |
| Spread | 30°（0–60） | 左右散布角=30° |
| Glow | true | 白色光晕=true |
| Glow color | 0 | 随字色；另外提供粉、蓝、金 |
| Glow strength | 0.3（0–3） | 光晕强度=0.3 |
| Glow radius | 0.5（0–1） | 光晕半径=0.5 |
| Wiggle | true | 轻微抖动=true |
| Wiggle fps | 8（4–24） | 抖动频率=8 |
| Wiggle amount | 0.8（0–2） | 抖动幅度=0.8 |
| Word pop | true | 逐字弹出=true；缺少逐字时间时按原项目的单词／中日韩双字分组估算 |
| Face player | true | 始终朝向玩家=true |
| Keep away | true | 靠近时后退=true，最小间距为Distance×0.6 |
| Stay on screen | false | 智能防离屏=false |
| Above ground | false | 保持视线以上=false |
| Split lines | true | 自动拆分合并歌词=true；中日韩句间空格拆句，短段合并，保留逐字时间 |
| 字色常量 | #F7E1D7 | 相同暖白色 |

## 按用户要求的两侧位置

每句在左／右侧交替生成，避免默认出现在正中；散布角仍为30°。原项目是在±Spread范围内随机偏转，可能接近中心，这里按用户明确要求优先选取左右侧角度。保留原项目向上3–13°的生成倾角。默认不固定跟随相机，保留世界空间位置；靠近时后退，始终朝向玩家。

## PEAK 专用扩展与未实现项

- 固定跟随视角默认false；附加翻译默认false；材质额外亮度默认1；整句模式；时间偏移0。额外整句入场效果默认“直接”，只保留原项目逐字显现；退场为向上淡出。
- 播放器来源、路径和端口、F9设置窗口及本机暂停属于PEAK适配，不对应Minecraft内置播放器默认项，恢复歌词默认时不改连接设置。
- 原项目Now playing=false、Cover=true、Smooth font=false、Text flash=false、Flash size=1。在PEAK中没有照搬歌曲HUD、封面、Minecraft像素字体或TextFlash渲染器，不设置无效的同名开关。原项目字体、OpenGL DeepGlow与Unity TMP/备用TextMesh的渲染结果不能视为像素一致。
- 穿墙／后景为PEAK现有深度扩展；默认允许穿墙、关闭后景，不把其声明为原项目同名选项。

1.1.1仅核对源码、修改、编译和打包；按用户要求未启动游戏或接口测试。1.1.0的居中位置测试不作为本版两侧位置的验收记录。

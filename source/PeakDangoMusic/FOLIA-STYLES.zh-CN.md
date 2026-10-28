# Folia歌词样式对照 · 1.2.0

上游：https://github.com/chthollyphile/folia-major
固定提交：`7a5f975e337e5ba13f55d72b5bc68983bf41c379`（0.7.23）。以下15种与上游内置清单逐项对应，直接构建原React/Pixi/Three渲染器。

| 中文名称 | ID | 上游注册入口 |
|---|---|---|
| 商籁 | `sonnet` | `src/components/visualizer/sonnet/entry.tsx` |
| 凝彩 | `tempera` | `src/components/visualizer/tempera/entry.tsx` |
| 绘光 | `lumiere` | `src/components/visualizer/lumiere/entry.tsx` |
| 花火 | `hanabi` | `src/components/visualizer/hanabi/entry.tsx` |
| 流光 | `classic` | `src/components/visualizer/classic/entry.tsx` |
| 心象 | `cadenza` | `src/components/visualizer/cadenza/entry.tsx` |
| 云阶 | `partita` | `src/components/visualizer/partita/entry.tsx` |
| 浮名 | `fume` | `src/components/visualizer/fume/entry.tsx` |
| 倾诉 | `tilt` | `src/components/visualizer/tilt/entry.tsx` |
| 回环 | `claddagh` | `src/components/visualizer/claddagh/entry.tsx` |
| 莫奈 | `monet` | `src/components/visualizer/monet/entry.tsx` |
| 时计 | `pendolo` | `src/components/visualizer/pendolo/entry.tsx` |
| 群唱 | `cappella` | `src/components/visualizer/cappella/entry.tsx` |
| 镜台 | `diorama` | `src/components/visualizer/diorama/entry.tsx` |
| 静止 | `still` | `src/components/visualizer/still/entry.tsx` |

F9 → Folia歌词样式：保留Dango默认项，新增15种原样式和按曲目随机选择。提供两侧悬浮、两侧屏幕、全屏舞台、大小、透明度、字体倍率、15/24/30/60帧、360p/540p/720p及兼容渲染选项。

原项目的样式设置保持各自默认；PEAK提供上述通用调节，不宣称包含原播放器的全部设置、第三方Folium模组或音乐播放功能。

现有网易云和LX的歌曲、进度、歌词直接送入渲染器；不更换或控制播放器。已有逐字时间优先，只有行时间时按文本长度估算。没有提供音频频谱、节拍／人声分析、封面／立绘时，不伪造这些输入，相关附加视觉元素可能缺省。

原项目全屏构图显示为PEAK舞台图像，因此屏幕占比、遮挡、字体与原播放器实际窗口可能不同。原Dango文字仍是独立显示方式；Folia准备过程中或失败时回退到Dango。

Folia与适配渲染器采用AGPL-3.0；Dango插件部分保留GPL-3.0，发行组合遵循GPLv3／AGPLv3第13条；完整源码、构建锁文件和各自许可随包提供。Electron及Chromium许可随运行时提供。

按用户要求未启动游戏、音乐播放器或Folia渲染进程验收；只完成源码核对、编译与打包。

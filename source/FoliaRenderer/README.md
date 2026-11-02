# PEAK Folia Renderer

适用于 PeakDangoMusic 1.3.0 的本地离屏歌词渲染组件，复用 folia-major 全部15种内置渲染器，不包含音乐播放页或音乐API服务器。

上游：<https://github.com/chthollyphile/folia-major>，提交 `7a5f975e337e5ba13f55d72b5bc68983bf41c379`，原版本0.7.23。原源码、版权声明和AGPL-3.0许可证保留在 `vendor/folia-major`。本目录新增适配代码采用AGPL-3.0。

组件只读取既有网易云／LX连接提供的曲目、播放位置和同步歌词，通过原React/Pixi/Three渲染器生成图像，由PEAK插件显示。无可见播放器窗口，不接收键鼠或访问账号／Cookie；远程网页请求被禁用。静态资源通过peak-folia://app专用协议直接读取安装文件，不开启HTTP服务器或监听端口；歌词和位图通过专属双向命名管道传递。

默认Dango不启动组件。Folia异常时回退到Dango并在F9提示；F8和切回Dango会停止组件。播放器暂停时保留当前位置，菜单、加载和设置界面以屏幕舞台接续。

## 构建

需要Windows x64、Node.js 24或更高版本、npm、Python3。

```powershell
npm ci --ignore-scripts --no-audit --no-fund
npm run build
python tools/package-runtime.py --output ../../artifacts/folia
```

最后一步下载ELECTRON.json固定的Electron运行时、核对SHA-256并整理运行目录和依赖许可，不会启动程序。前端构建只使用此目录锁文件和已附上游源码。

## 样式与输入

商籁、凝彩、绘光、花火、流光、心象、云阶、浮名、倾诉、回环、莫奈、时计、群唱、镜台、静止。styles.json对应上游src/types/visualizerModes.ts。

优先保留实际逐字时间；普通LRC按行时长与文本长度估算。没有音频频谱、节拍分析、封面或立绘时不伪造输入，相关附加反应效果或图像可能缺省，歌词排版和时间驱动动画仍由原渲染器完成。

原项目为全屏播放器；PEAK提供两侧悬浮、两侧屏幕和全屏舞台。按用户要求只编译、打包，没有运行游戏、播放器或离屏渲染进程测试。

## 许可证

Folia及本目录适配代码为AGPL-3.0；Dango插件部分保留GPL-3.0，发行组合依照GPLv3／AGPLv3第13条，随附两部分对应源码和许可。Electron为MIT，Chromium等组件许可见Runtime中的LICENSE与LICENSES.chromium.html；前端依赖见App/THIRD-PARTY-NOTICES.txt。

## 1.2.1 修复

桥接引导与原样式模块分开加载；只接收当前选择对应且已完成绘制的第二版协议帧。样式异常立即回退Dango，30秒启动超时重启。静态或暂停样式定时重绘保活。启动、模块和渲染错误记录到游戏目录 BepInEx/PeakDangoMusic-Folia.log（128KiB滚动），F9同时给出原因。DLL与App必须配套升级。

用户截图确认原页面在http://127.0.0.1随机端口加载时返回ERR_FAILED(-2)。1.2.1取消HTTP依赖，使用Electron标准安全自定义协议直接返回已安装静态文件；不修改系统代理、防火墙或播放器网络。

## 1.2.2

实际复现Windows隐藏GUI Electron的stdin立即EOF，导致组件提前退出，页面加载被中断而返回ERR_FAILED。改为随机专用命名管道；标准输出只丢弃控制台噪声，不参与帧协议。已获用户授权，仅以示例歌词诊断隐藏组件，未启动游戏或播放器。配套DLL与App使用peakProtocol3，帧仍为DANGOF02。

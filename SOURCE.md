# 源码与许可证

2.3.0完整安装包只包含运行组件、安装脚本和文档；完整对应源码为 PeakDangoMusic-2.3.0-source.zip。本地构建尚未自动上传GitHub。

- source/PeakDangoMusic：PEAK主界面、播放引擎、加密账号存储及原有Dango/Folia接入。
- source/MuoniumBridge：上游23个原生服务文件、PEAK私有stdio适配、依赖锁定和可复现构建脚本。
- source/FoliaRenderer：既有Folia源码、前端适配和运行时构建。
- source/NetEaseBridge与旧LX/CDP/SMTC读取器仅保留为历史源码，本版不启动这些旧路径。

Dango/插件GPL-3.0，Folia及渲染适配AGPL-3.0，Muonium原生服务Apache-2.0。Java运行时GPL-2.0 with Classpath Exception及其第三方声明随Muonium/Runtime/legal提供；Gson和ZXing为Apache-2.0。

账号凭证、用户曲库、游戏文件、临时实机检查组件、诊断截图和缓存均不进入源码或安装包。项目地址：https://github.com/ZHIZHU4410/PeakDangoMusic 。

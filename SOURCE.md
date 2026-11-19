# 源码与许可证

2.3.4完整安装包只包含运行组件、安装脚本和文档；完整对应源码为 PeakDangoMusic-2.3.4-source.zip。本地构建尚未自动上传GitHub。

- source/PeakDangoMusic：2026-11-18合并用户提供的跨电脑2.3.0 C#工程，包含DangoMusic.Core、DangoMusic.Diagnostics、PeakDangoMusic子目录与嵌入素材；PEAK主界面、播放引擎、加密账号存储及原有Dango/Folia接入。
- source/MuoniumBridge：上游原生服务、各平台适配与LX沙箱、PEAK私有stdio协议、依赖锁定和可复现构建脚本。第三方Rhino/Closure对应源码获取地址及许可见NOTICE.md。
- source/FoliaRenderer：既有Folia源码、前端适配和运行时构建。
- source/NetEaseBridge与旧LX/CDP/SMTC读取器仅保留为历史源码，本版不启动这些旧路径。

Dango/插件GPL-3.0，Folia及渲染适配AGPL-3.0，Muonium原生服务Apache-2.0。Java运行时GPL-2.0 with Classpath Exception及其第三方声明随Muonium/Runtime/legal提供；Gson和ZXing为Apache-2.0。

账号凭证、用户曲库、游戏文件、临时实机检查组件、诊断截图和缓存均不进入源码或安装包。项目地址：https://github.com/ZHIZHU4410/PeakDangoMusic 。

跨电脑合并详情见docs/MERGE-20261118.md；原始输入文件夹保持不变，导入前工程另存本机backups/merge-other-pc-20261118。

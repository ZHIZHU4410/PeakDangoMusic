namespace PeakDangoMusic;

// Built-in renderer registry at folia-major 7a5f975e337e5ba13f55d72b5bc68983bf41c379.
internal static class FoliaStyles
{
    internal static readonly string[] Ids={"dango","sonnet","tempera","lumiere","hanabi","classic","cadenza","partita","fume","tilt","claddagh","monet","pendolo","cappella","diorama","still","random"};
    internal static readonly string[] Labels={"Dango · 原有悬浮","商籁 · Sonnet","凝彩 · Tempera","绘光 · Lumiere","花火 · Hanabi","流光 · Classic","心象 · Cadenza","云阶 · Partita","浮名 · Fume","倾诉 · Tilt","回环 · Claddagh","莫奈 · Monet","时计 · Pendolo","群唱 · Cappella","镜台 · Diorama","静止 · Still","每首随机样式"};
    internal static readonly string[] Descriptions={"原有左右世界歌词与菜单文字浮层。","海报排版、镜头与角标构成的文字PV。","上下色块、切割线与块面文字构图。","光束、雾场和线条照亮歌词。","花火、彩色块与文字动效编排。","居中逐词点亮与呼吸浮动。","散落字符云与当前字符焦点。","逐行错落的阶梯式歌词。","大字主体与淡化上下文叠层。","主行与倾斜副行的错落排版。","椭圆轨道、旋转与环形文字。","左侧关键词与海报式歌词轨道。","表盘、齿轮与右侧歌词。","左右对话气泡与历史消息。","不同景深的歌词与3D镜头飞行。","上一句、当前句、下一句静态显示。","按曲目稳定选取15种Folia样式之一。"};
    internal static int Index(string id){int index=Array.IndexOf(Ids,id);return index<0?0:index;}
    internal static string Resolve(string id,string song)
    {
        if(id!="random")return Index(id)>0?id:"classic";
        uint hash=2166136261;foreach(char c in song)hash=unchecked((hash^c)*16777619);
        return Ids[1+(int)(hash%15)];
    }
}

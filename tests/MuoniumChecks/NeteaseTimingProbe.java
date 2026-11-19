import com.muoniumplayer.core.music.source.*;
import com.google.gson.*;
import java.util.*;
public class NeteaseTimingProbe {
 public static void main(String[] args){
  var api=new MuNeteaseApi();
  var tracks=api.search(args.length>0?args[0]:"Faded",1,0);if(tracks.isEmpty())throw new AssertionError("search empty");
  var id=tracks.get(0).getId();var actual=api.getLyric(tracks.get(0));System.out.println("current adapter timedLines="+actual.getLines().stream().filter(l->l.getWords()!=null&&!l.getWords().isEmpty()).count());
  for(String route:List.of("/weapi/song/lyric","/weapi/song/lyric/v1")){
   var data=api.callJson(route,Map.of("id",id,"lv","0","kv","0","tv","0","rv","0","yv","0","ytv","0","yrv","0"));
   var yrc=data.has("yrc")&&data.get("yrc").isJsonObject()?data.getAsJsonObject("yrc"):new JsonObject();
   String text=yrc.has("lyric")?yrc.get("lyric").getAsString():"";
   var lines=LyricParser.INSTANCE.parse("",null,text);
   long count=lines.stream().filter(l->l.getWords()!=null&&!l.getWords().isEmpty()).count();
   System.out.println(route+" code="+data.get("code")+" timedLines="+count);
   lines.stream().filter(l->l.getStartMs()>30000&&l.getWords()!=null&&!l.getWords().isEmpty()).limit(2).forEach(l->{System.out.println("line="+l.getStartMs()+" duration="+l.getDurationMs());System.out.println(l.getWords().stream().limit(8).map(w->w.getStartMs()+":"+w.getDurationMs()+":"+w.getText().length()).toList());});
  }
 }
}

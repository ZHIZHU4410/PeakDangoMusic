import com.google.gson.*;
import com.muoniumplayer.core.music.source.*;
import com.muoniumplayer.core.ncm.kugou.*;
import java.util.*;
import java.util.regex.*;

/** Read-only, source-ID-specific comments. Never searches another catalogue by title. */
final class PlatformComments {
 static String str(JsonObject object,String...keys){for(String key:keys)if(object!=null&&object.has(key)&&object.get(key).isJsonPrimitive())return object.get(key).getAsString();return "";}
 static JsonObject get(String url,Map<String,String> query,String referer){
  var response=new MuHttp().get(url,query,Map.of("Referer",referer),null);
  return JsonParser.parseString(response.getBody()).getAsJsonObject();
 }
 static JsonObject handle(JsonObject request,PeakMuoniumBridge bridge){
  String source=str(request,"source"),id=str(request,"trackId");JsonObject raw;
  switch(source){
   case "ncm":case "netease":
    if(!id.matches("[0-9]{1,24}"))throw new IllegalArgumentException("comment id");
    raw=get("https://music.163.com/api/v1/resource/comments/R_SO_4_"+id,Map.of("limit","100","offset","0"),"https://music.163.com/");break;
   case "qq":case "tencent":
    var track=bridge.tracks.get("qq:"+id);raw=bridge.api.getQq().comments(track!=null?track.getId():id);break;
   case "kugou":{
    var kg=KugouAdapter.tracks.get(id);String mix=kg!=null?kg.requestAlbumAudioId():"";
    if(mix.isEmpty()){var out=new JsonObject();out.addProperty("message","酷狗曲目缺少MixSongID，请重新搜索或打开歌单后播放");out.add("comments",new JsonArray());return out;}
    var params=KuGouHttp.deviceParams();params.putAll(Map.of("ver","6","mixsongid",mix,"p","1","pagesize","30","extdata","0","code","fc4be23b4e972707f36b8a828a93ba8a"));
    params.put("need_show_image","1");params.put("show_classify","1");params.put("show_hotword_list","1");
    raw=KuGouHttp.postJson(KuGouHttp.GATEWAY,"/mcomment/v1/cmtlist",params,"",KuGouHttp.Sign.ANDROID,Map.of());break;
   }
   case "bilibili":case "bilibili-native":{
    var matcher=Pattern.compile("BV[0-9A-Za-z]{10}").matcher(id);
    String aid;
    if(matcher.find()){var video=get("https://api.bilibili.com/x/web-interface/view",Map.of("bvid",matcher.group()),"https://www.bilibili.com/");aid=str(video.getAsJsonObject("data"),"aid");}
    else aid=id.startsWith("av")?id.substring(2):id;
    if(!aid.matches("[0-9]+"))throw new IllegalArgumentException("comment video id");
    raw=get("https://api.bilibili.com/x/v2/reply",Map.of("type","1","oid",aid,"sort","2","pn","1"),"https://www.bilibili.com/");break;
   }
   default:{var out=new JsonObject();out.add("comments",new JsonArray());out.addProperty("message","当前平台暂未接入评论接口");return out;}
  }
  if(raw==null)throw new IllegalStateException("评论请求失败");
  if(source.equals("kugou")&&raw.has("err_code")&&!str(raw,"err_code").equals("0")){var out=new JsonObject();out.add("comments",new JsonArray());out.addProperty("message","酷狗评论接口返回错误码 "+str(raw,"err_code")+"，请稍后重试");return out;}
  if(raw.has("code")&&!Set.of("0","200").contains(str(raw,"code")))throw new IllegalStateException("平台评论请求被拒");
  var out=new JsonObject();var rows=new JsonArray();collect(raw,rows,0);out.add("comments",rows);return out;
 }
 static void collect(JsonElement value,JsonArray rows,int depth){
  if(value==null||value.isJsonNull()||depth>12||rows.size()>=100)return;
  if(value.isJsonArray()){for(var item:value.getAsJsonArray())collect(item,rows,depth+1);return;}
  if(!value.isJsonObject())return;var object=value.getAsJsonObject();
  String body=str(object,"rootcommentcontent","content","comment_content");
  if(body.isEmpty()&&object.has("member")&&object.has("content")&&object.get("content").isJsonObject())body=str(object.getAsJsonObject("content"),"message");
  if(!body.isEmpty()){
   JsonObject user=object.has("user")&&object.get("user").isJsonObject()?object.getAsJsonObject("user"):object.has("member")&&object.get("member").isJsonObject()?object.getAsJsonObject("member"):object;
   var row=new JsonObject();row.addProperty("content",body);var person=new JsonObject();
   String name=str(user,"nickname","nick","uname","user_name","username");person.addProperty("nickname",name.isEmpty()?"音乐用户":name);
   person.addProperty("avatarUrl",str(user,"avatarUrl","avatarurl","avatar","user_pic","user_pic_url"));row.add("user",person);
   String likes=str(object,"likedCount","praisenum","like_count","like","liked_count");try{row.addProperty("likedCount",Integer.parseInt(likes));}catch(Exception ignored){row.addProperty("likedCount",0);}rows.add(row);return;
  }
  for(var entry:object.entrySet())if(entry.getValue().isJsonArray()||entry.getValue().isJsonObject())collect(entry.getValue(),rows,depth+1);
 }
}

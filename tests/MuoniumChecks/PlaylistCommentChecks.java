import com.google.gson.*;
import com.muoniumplayer.core.music.source.*;
import com.muoniumplayer.core.ncm.kugou.*;
import java.util.*;
public class PlaylistCommentChecks {
 public static void main(String[] args)throws Exception{
  var list=MuQQApi.parseUserPlaylists(JsonParser.parseString("{\"data\":{\"mydiss\":{\"list\":[{\"dissid\":\"123\",\"dissname\":\"合成歌单\",\"picurl\":\"https://example.invalid/a.jpg\",\"songnum\":4}]}}}").getAsJsonObject());
  if(list.size()!=1||!list.get(0).getAsJsonObject().get("id").getAsString().equals("123"))throw new AssertionError("QQ personal schema");
  if(list.get(0).getAsJsonObject().get("trackCount").getAsInt()!=4)throw new AssertionError("QQ count");
  var cloud=new KuGouMusicApi.Playlist();cloud.id="abc";cloud.collectionId="abc";
  var own=new KuGouMusicApi.Playlist();own.id="2";own.listId="2";
  var legacy=new KuGouMusicApi.Playlist();legacy.id="3";
  var rows=KugouAdapter.playlists(List.of(cloud,own,legacy));
  for(int i=0;i<3;i++)if(!rows.get(i).getAsJsonObject().get("id").getAsString().startsWith(List.of("cloud:","user:","special:").get(i)))throw new AssertionError("Kugou ID spaces");
  var comments=new JsonArray();PlatformComments.collect(JsonParser.parseString("{\"comment\":{\"commentlist\":[{\"rootcommentcontent\":\"合成QQ评论\",\"nick\":\"合成昵称\",\"praisenum\":5}]}}"),comments,0);
  if(comments.size()!=1||comments.get(0).getAsJsonObject().get("likedCount").getAsInt()!=5)throw new AssertionError("QQ comment shape");
  var unsupported=new JsonObject();unsupported.addProperty("op","comments");unsupported.addProperty("source","spotify");unsupported.addProperty("trackId","123");
  if(PlatformComments.handle(unsupported,new PeakMuoniumBridge()).getAsJsonArray("comments").size()!=0)throw new AssertionError("No cross-platform fallback");
  var bridge=new PeakMuoniumBridge();var c=JsonParser.parseString("{\"op\":\"restore\",\"credentials\":{\"qqId\":\"10001\",\"qqKey\":\"synthetic-key\",\"qqLoginType\":1}}").getAsJsonObject();
  if(bridge.handle(c).getAsJsonObject("credentials").get("qqLoginType").getAsInt()!=1)throw new AssertionError("WeChat type persistence");
  System.out.println("Playlist/comments/login checks: 8 passed");
 }
}

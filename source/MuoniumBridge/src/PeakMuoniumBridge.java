import com.muoniumplayer.core.music.source.*;
import com.google.gson.*;
import com.google.zxing.*;
import com.google.zxing.qrcode.QRCodeWriter;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.awt.image.BufferedImage;
import javax.imageio.ImageIO;

/** PEAK adapter; upstream service code is in vendor, Apache-2.0. No listening socket. */
public final class PeakMuoniumBridge {
    final Gson gson=new Gson();
    final MuMusicApi api=new MuMusicApi();
    final Map<String,QrCode> qr=new HashMap<>();
    final Map<String,Track> tracks=new LinkedHashMap<>();
    long qrAt;
    com.muoniumplayer.core.ncm.qq.QQWechatLogin.Session wechatQr;
    boolean wechatActive;
    public static void main(String[] args)throws Exception{
        if(args.length!=1)return;
        long parent=Long.parseLong(args[0]);
        Thread watch=new Thread(()->{while(ProcessHandle.of(parent).map(ProcessHandle::isAlive).orElse(false)){try{Thread.sleep(1000);}catch(Exception ignored){}}System.exit(0);});
        watch.setDaemon(true);watch.start();
        var bridge=new PeakMuoniumBridge();
        try(var input=new BufferedReader(new InputStreamReader(System.in,StandardCharsets.UTF_8));var output=new PrintWriter(new OutputStreamWriter(System.out,StandardCharsets.UTF_8),true)){
            String line;
            while((line=readBounded(input))!=null){
                JsonObject response=new JsonObject();
                try{var request=JsonParser.parseString(line).getAsJsonObject();response.add("id",request.get("id"));response.add("data",bridge.handle(request));response.addProperty("ok",true);}
                catch(Exception e){response.addProperty("ok",false);response.addProperty("error","平台请求失败（"+e.getClass().getSimpleName()+"），请重试或检查登录/网络");if(e instanceof IllegalStateException && Set.of("audio unavailable","FFmpeg required","host audio cache required","audio decode failed","audio decode timeout").contains(e.getMessage()))response.addProperty("errorCode",e.getMessage());}
                output.println(bridge.gson.toJson(response));
            }
        }
    }
    static String readBounded(java.io.Reader in)throws IOException{
        StringBuilder s=new StringBuilder();int c;
        while((c=in.read())!=-1){if(c=='\n')break;if(s.length()>=262144)throw new IOException("request too large");s.append((char)c);}
        return c==-1&&s.length()==0?null:s.toString();
    }
    MusicSource source(String s){return s.equals("qq")?MusicSource.QQ:MusicSource.NETEASE;}
    String text(JsonObject q,String k){return q.has(k)&&!q.get(k).isJsonNull()?q.get(k).getAsString():"";}
    JsonObject handle(JsonObject q)throws Exception{
        String op=text(q,"op"),platform=text(q,"source");MusicSource source=source(platform);
        if(op.equals("comments"))return PlatformComments.handle(q,this);
        if(platform.equals("lx-native"))return LxAdapter.handle(q,this);
        if(platform.equals("youtube-native")){var result=YouTubeAdapter.handle(q);result.add("credentials",credentials());return result;}
        if(platform.equals("bilibili-native")){var result=BilibiliAdapter.handle(q);result.add("credentials",credentials());return result;}
        if(platform.equals("kugou")){var result=KugouAdapter.handle(q);result.add("credentials",credentials());return result;}
        JsonObject out=new JsonObject();
        switch(op){
            case "hello":out.addProperty("protocol",1);break;
            case "restore":{ // Partial imports preserve the other platforms; persist only through host DPAPI.
                var c=q.getAsJsonObject("credentials");
                if(c!=null){if(c.has("youtube")&&c.get("youtube").isJsonObject())com.muoniumplayer.core.ncm.music.YouTubeSessionStore.restoreState(c.getAsJsonObject("youtube"));if(c.has("bilibili")&&c.get("bilibili").isJsonObject())com.muoniumplayer.core.ncm.music.BilibiliSessionStore.state=c.getAsJsonObject("bilibili");if(c.has("kugou")&&c.get("kugou").isJsonObject())com.muoniumplayer.core.ncm.kugou.PeakDeviceStore.state=c.getAsJsonObject("kugou");if(c.has("netease"))api.getNetease().setCookie(text(c,"netease"));if(c.has("qqId"))api.getQq().setMusicid(text(c,"qqId"));if(c.has("qqKey"))api.getQq().setMusicKey(text(c,"qqKey"));if(c.has("qqLoginType"))api.getQq().setLoginType(c.get("qqLoginType").getAsInt());}
                out.add("credentials",credentials());break;
            }
            case "qr":{
                if(platform.equals("qq")){
                    wechatActive=text(q,"loginMethod").equals("wechat");wechatQr=null;
                    if(wechatActive){wechatQr=com.muoniumplayer.core.ncm.qq.QQWechatLogin.create();qrAt=System.currentTimeMillis();String data=wechatQr.imageDataUrl;out.addProperty("image",data.substring(data.indexOf(',')+1));out.addProperty("state","WAITING");break;}
                }
                QrCode code=api.createQrCode(source);qr.put(platform,code);qrAt=System.currentTimeMillis();
                String content=code.getQrContent();
                if(content.startsWith("data:image/"))out.addProperty("image",content.substring(content.indexOf(',')+1));
                else{
                    var matrix=new QRCodeWriter().encode(content,BarcodeFormat.QR_CODE,280,280,Map.of(EncodeHintType.MARGIN,4));
                    var image=new BufferedImage(280,280,BufferedImage.TYPE_INT_RGB);
                    for(int y=0;y<280;y++)for(int x=0;x<280;x++)image.setRGB(x,y,matrix.get(x,y)?0:0xffffff);
                    var bytes=new ByteArrayOutputStream();ImageIO.write(image,"png",bytes);out.addProperty("image",Base64.getEncoder().encodeToString(bytes.toByteArray()));
                }
                out.addProperty("state","WAITING");break;
            }
            case "poll":{
                if(platform.equals("qq")&&wechatActive){
                    if(wechatQr==null||System.currentTimeMillis()-qrAt>180000){out.addProperty("state","EXPIRED");break;}
                    var status=com.muoniumplayer.core.ncm.qq.QQWechatLogin.poll(wechatQr.uuid);
                    String state=switch(status.status){case 405->"CONFIRMED";case 404->"SCANNED";case 402->"EXPIRED";case 403->"ERROR";default->"WAITING";};
                    if(state.equals("CONFIRMED")){var credential=com.muoniumplayer.core.ncm.qq.QQWechatLogin.exchange(status.code);api.getQq().setMusicid(credential.musicId);api.getQq().setMusicKey(credential.musicKey);api.getQq().setLoginType(1);out.add("credentials",credentials());wechatQr=null;}
                    out.addProperty("state",state);break;
                }
                var code=qr.get(platform);
                if(code==null||System.currentTimeMillis()-qrAt>180000){out.addProperty("state","EXPIRED");break;}
                var state=api.checkQrCode(source,code);out.addProperty("state",state.name());
                if(state==QrLoginState.CONFIRMED){if(platform.equals("qq"))api.getQq().setLoginType(0);qr.remove(platform);out.add("credentials",credentials());}
                break;
            }
            case "account":{
                if(source==MusicSource.QQ){if(api.getQq().getLoggedIn()){var user=api.getQq().getUserInfo();out.addProperty("name",user.getNickname());out.addProperty("avatar",user.getAvatarUrl());out.addProperty("loggedIn",true);}else out.addProperty("loggedIn",false);}
                else{var data=api.getNetease().callJson("/weapi/w/nuser/account/get",Map.of());var value=data.get("profile");var profile=value!=null&&value.isJsonObject()?value.getAsJsonObject():null;out.addProperty("loggedIn",profile!=null);if(profile!=null){out.addProperty("name",text(profile,"nickname"));out.addProperty("avatar",text(profile,"avatarUrl"));}}
                break;
            }
            case "logout":{
                if(source==MusicSource.QQ)api.getQq().clearLogin();else api.getNetease().clearLogin();
                qr.remove(platform);out.add("credentials",credentials());out.addProperty("loggedIn",false);break;
            }
            case "search":{
                int page=q.has("page")?Math.max(1,q.get("page").getAsInt()):1;
                var rows=source==MusicSource.QQ?api.getQq().search(text(q,"query"),20,page):api.getNetease().search(text(q,"query"),20,(page-1)*20);
                out.add("tracks",list(rows));break;
            }
            case "discover":{
                if(source==MusicSource.QQ)out.add("playlists",gson.toJsonTree(api.getQq().getRecommendPlaylists(20)));
                else{
                    var data=api.getNetease().callJson("/weapi/personalized/playlist",Map.of("limit","20","total","true","n","1000"));
                    out.add("playlists",data.get("result")!=null?data.get("result"):new JsonArray());
                }
                break;
            }
            case "daily":{
                if(source==MusicSource.QQ)throw new IllegalArgumentException("daily source");
                if(api.getNetease().getLoginUid()==null)throw new IllegalStateException("login required");
                out.add("tracks",list(api.getNetease().getDailyRecommendSongs()));break;
            }
            case "library":{
                if(source==MusicSource.QQ){out.add("playlists",api.getQq().getUserPlaylists());}
                else{var uid=api.getNetease().getLoginUid();if(uid==null)throw new IllegalStateException("login required");out.add("playlists",gson.toJsonTree(api.getNetease().getUserPlaylists(uid,100,0)));}
                break;
            }
            case "playlist":{
                String id=text(q,"playlistId");if(!id.matches("[0-9]{1,24}"))throw new IllegalArgumentException("playlist id");
                var rows=source==MusicSource.QQ?api.getQq().getPlaylistTracks(id,200):api.getNetease().getPlaylistTracks(id,200);
                out.add("tracks",list(rows));break;
            }
            case "resolve":{
                Track track=tracks.get(platform+":"+text(q,"trackId"));
                if(track==null&&q.has("track")){var t=q.getAsJsonObject("track");track=new Track(source,text(t,"id"),source==MusicSource.QQ?text(t,"id"):null,text(t,"title"),text(t,"artist"),text(t,"album"),0,null,false);}
                if(track==null)throw new IllegalArgumentException("track missing");
                SongUrl url=source==MusicSource.QQ
                    ? api.getQq().getSongUrl(track,AudioQuality.HIGH,true)
                    : api.getNetease().getSongUrl(track,AudioQuality.HIGH);
                if(!url.getAvailable())throw new IllegalStateException("audio unavailable");
                String format=url.getFormat();
                String playbackUrl=url.getUrl();
                if(format!=null && Set.of("m4a","aac","flac").contains(format.toLowerCase(Locale.ROOT))){
                    playbackUrl=AudioCodec.mp3(playbackUrl,source==MusicSource.QQ?"https://y.qq.com/":"https://music.163.com/");
                    format="mp3";
                }
                out.addProperty("url",playbackUrl);out.addProperty("format",format);out.addProperty("trial",url.isTrial());out.addProperty("trialStartMs",url.getTrialStartMs());
                try{Lyric lyric=api.getLyric(track);out.add("lines",gson.toJsonTree(lyric.getLines()));
                    // Preserve raw syllable data for the same parser used in PEAK 2.1.2.
                    out.addProperty("lrc",lyric.getLrc());out.addProperty("yrc",lyric.getYrc());out.addProperty("tlyric",lyric.getTranslated());
                }catch(Exception ignored){out.add("lines",new JsonArray());}
                break;
            }
            default:throw new IllegalArgumentException("unsupported operation");
        }
        return out;
    }
    static String qrImage(String content)throws Exception{
        var matrix=new QRCodeWriter().encode(content,BarcodeFormat.QR_CODE,280,280,Map.of(EncodeHintType.MARGIN,4));var image=new BufferedImage(280,280,BufferedImage.TYPE_INT_RGB);
        for(int y=0;y<280;y++)for(int x=0;x<280;x++)image.setRGB(x,y,matrix.get(x,y)?0:0xffffff);
        var bytes=new ByteArrayOutputStream();ImageIO.write(image,"png",bytes);return Base64.getEncoder().encodeToString(bytes.toByteArray());
    }
    JsonObject credentials(){var c=new JsonObject();c.add("youtube",com.muoniumplayer.core.ncm.music.YouTubeSessionStore.exportState());c.addProperty("netease",api.getNetease().getCookie());c.addProperty("qqId",api.getQq().getMusicid());c.addProperty("qqKey",api.getQq().getMusicKey());c.addProperty("qqLoginType",api.getQq().getLoginType());if(com.muoniumplayer.core.ncm.kugou.PeakDeviceStore.state!=null)c.add("kugou",com.muoniumplayer.core.ncm.kugou.PeakDeviceStore.state);c.add("bilibili",com.muoniumplayer.core.ncm.music.BilibiliSessionStore.state);return c;}
    JsonArray list(List<Track> rows){
        var out=new JsonArray();
        for(Track t:rows){String platform=t.getSource()==MusicSource.QQ?"qq":"ncm";String stableId=t.getSource()==MusicSource.QQ?t.getMid():t.getId();if(stableId==null||stableId.isEmpty())continue;tracks.put(platform+":"+stableId,t);var row=new JsonObject();row.addProperty("id",stableId);row.addProperty("title",t.getName());row.addProperty("artist",t.getArtists());row.addProperty("album",t.getAlbum());row.addProperty("cover",t.getCoverUrl());row.addProperty("source",platform);out.add(row);}
        while(tracks.size()>1000)tracks.remove(tracks.keySet().iterator().next());return out;
    }
}

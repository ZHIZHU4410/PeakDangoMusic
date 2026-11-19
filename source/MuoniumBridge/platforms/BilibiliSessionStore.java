package com.muoniumplayer.core.ncm.music;
import com.google.gson.*;
import java.util.*;
/** PEAK adaptation: state stays in memory and is persisted only by host DPAPI. */
public final class BilibiliSessionStore {
 public static JsonObject state=new JsonObject();
 static final class Session {
  final boolean loggedIn,vip;final String cookieHeader,csrf,userId,nickname,avatarUrl,vipLabel;final int vipType;
  Session(){cookieHeader=getCookieHeader();loggedIn=cookieHeader.contains("SESSDATA=");csrf=text("csrf");userId=text("userId");nickname=text("nickname");avatarUrl=text("avatarUrl");vip=state.has("vip")&&state.get("vip").getAsBoolean();vipLabel=text("vipLabel");vipType=state.has("vipType")?state.get("vipType").getAsInt():0;}
 }
 static String text(String key){return state.has(key)&&!state.get(key).isJsonNull()?state.get(key).getAsString():"";}
 static Session snapshot(){return new Session();}
 static String getCookieHeader(){return text("cookies");}
 static boolean isLoggedIn(){return snapshot().loggedIn;}
 static String getCsrfToken(){return text("csrf");}
 static String getUserId(){return text("userId");}
 static synchronized void clear(){state=new JsonObject();}
 static synchronized void saveCookies(Map<String,String> incoming){
  Map<String,String> cookies=new TreeMap<>();for(String part:getCookieHeader().split(";")){int i=part.indexOf('=');if(i>0)cookies.put(part.substring(0,i).trim(),part.substring(i+1).trim());}cookies.putAll(incoming);
  state.addProperty("cookies",joinCookies(cookies));state.addProperty("csrf",cookies.getOrDefault("bili_jct",""));
 }
 static synchronized void saveProfile(String id,String nick,String avatar,boolean vip,int vipType,String vipLabel){state.addProperty("userId",id);state.addProperty("nickname",nick);state.addProperty("avatarUrl",avatar);state.addProperty("vip",vip);state.addProperty("vipType",vipType);state.addProperty("vipLabel",vipLabel);}
 static String joinCookies(Map<String,String> cookies){var parts=new ArrayList<String>();for(var e:cookies.entrySet()){if(e.getKey().matches("[A-Za-z0-9_-]+")&&e.getValue()!=null&&!e.getValue().contains("\n")&&!e.getValue().contains("\r")&&!e.getValue().contains(";"))parts.add(e.getKey()+"="+e.getValue());}Collections.sort(parts);return String.join("; ",parts);}
}

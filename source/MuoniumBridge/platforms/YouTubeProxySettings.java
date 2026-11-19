package com.muoniumplayer.core.ncm.music;
import java.net.*;
/** Optional, YouTube-only HTTP proxy, supplied by the PEAK host. */
public final class YouTubeProxySettings {
 public static String getEndpoint(){return System.getProperty("peak.youtubeProxy", "").trim();}
 public static boolean isEnabled(){return createInetSocketAddress()!=null;}
 public static InetSocketAddress createInetSocketAddress(){
  try {String value=getEndpoint();if(value.isEmpty())return null;
   URI uri=URI.create(value.contains("://")?value:"http://"+value);
   if(!"http".equals(uri.getScheme())||uri.getHost()==null||uri.getUserInfo()!=null||uri.getPort()<1||uri.getPort()>65535)return null;
   return InetSocketAddress.createUnresolved(uri.getHost(),uri.getPort());
  }catch(Exception ignored){return null;}
 }
}

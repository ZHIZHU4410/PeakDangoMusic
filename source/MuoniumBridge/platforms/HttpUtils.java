package com.muoniumplayer.core.utils.network;
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.Map;
/** Minimal PEAK transport adapter for the original KuGou service. No game/UI dependency. */
public final class HttpUtils {
 public static InputStream request(String url,String body,Map<String,String> headers,String method)throws IOException{return request(url,body,headers,method,"application/json");}
 public static InputStream request(String url,String body,Map<String,String> headers,String method,String contentType)throws IOException{return request(url,body,headers,method,contentType,10000,20000);}
 public static InputStream request(String url,String body,Map<String,String> headers,String method,String contentType,int connectTimeout,int readTimeout)throws IOException{
  var uri=URI.create(url);if(!uri.getScheme().equals("http")&&!uri.getScheme().equals("https"))throw new IOException("scheme");
  var connection=(HttpURLConnection)uri.toURL().openConnection();connection.setConnectTimeout(Math.min(30000,Math.max(1000,connectTimeout)));connection.setReadTimeout(Math.min(35000,Math.max(1000,readTimeout)));connection.setRequestMethod(method);
  if(headers!=null)headers.forEach(connection::setRequestProperty);
  if(body!=null){connection.setDoOutput(true);if(contentType!=null)connection.setRequestProperty("Content-Type",contentType);try(var out=connection.getOutputStream()){out.write(body.getBytes(StandardCharsets.UTF_8));}}
  try(var input=connection.getResponseCode()<400?connection.getInputStream():connection.getErrorStream();var out=new ByteArrayOutputStream()){
   if(input==null)throw new IOException("empty response");byte[] buffer=new byte[8192];int n;
   while((n=input.read(buffer))>0){if(out.size()+n>4*1024*1024)throw new IOException("response too large");out.write(buffer,0,n);}return new ByteArrayInputStream(out.toByteArray());
  }finally{connection.disconnect();}
 }
 public static String mapToString(String url,Map<String,String> params,String delimiter){var out=new StringBuilder(url).append(delimiter);for(var entry:params.entrySet()){if(out.charAt(out.length()-1)!='?')out.append('&');out.append(URLEncoder.encode(entry.getKey(),StandardCharsets.UTF_8)).append('=').append(URLEncoder.encode(entry.getValue(),StandardCharsets.UTF_8));}return out.toString();}
 public static String readString(InputStream input)throws IOException{try(input){return new String(input.readAllBytes(),StandardCharsets.UTF_8);}}
}

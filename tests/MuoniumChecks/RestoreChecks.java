import com.google.gson.*;
/** No network or real account data: exercise partial credential import on the actual bridge. */
public final class RestoreChecks {
 public static void main(String[] args)throws Exception {
  var bridge=new PeakMuoniumBridge();
  var request=JsonParser.parseString("{\"op\":\"restore\",\"credentials\":{\"netease\":\"synthetic-ncm\",\"qqId\":\"10001\",\"qqKey\":\"synthetic-old\"}}").getAsJsonObject();
  bridge.handle(request);
  request=JsonParser.parseString("{\"op\":\"restore\",\"source\":\"qq\",\"credentials\":{\"qqId\":\"10002\",\"qqKey\":\"synthetic-new\"}}").getAsJsonObject();
  var snapshot=bridge.handle(request).getAsJsonObject("credentials");
  if(!snapshot.get("netease").getAsString().equals("synthetic-ncm"))throw new AssertionError("QQ import cleared NetEase");
  if(!snapshot.get("qqId").getAsString().equals("10002"))throw new AssertionError("QQ account not updated");
  if(!snapshot.get("qqKey").getAsString().equals("synthetic-new"))throw new AssertionError("QQ credential not updated");
  System.out.println("Partial restore checks: 3 passed; full snapshot available for host DPAPI");
 }
}

package com.muoniumplayer.core.ncm.kugou;

import com.google.gson.JsonParser;

public final class KugouLibraryChecks {
    public static void main(String[] args) {
        var grouped = parse("""
                {"status":1,"data":{"info":{
                    "love":[{"listid":2,"listname":"我喜欢","m_count":65}],
                    "self":[{"listid":"42","name":"自建歌单"}],
                    "collect":[{"global_collection_id":"collection_1","title":"收藏歌单","song_count":75}],
                    "list":[{"list_id":42,"name":"自建歌单"}]
                }}}
                """);
        check(grouped.size() == 3, "Grouped records must be merged without duplicates");
        check(grouped.stream().anyMatch(list -> list.listId.equals("2") && list.trackCount == 65),
                "Preserve local playlist ID and count");
        check(grouped.stream().anyMatch(list -> list.collectionId.equals("collection_1") && list.trackCount == 75),
                "Preserve cloud playlist ID and count");
        check(parse("{\"data\":{\"info\":[{\"listid\":1,\"name\":\"flat\"}]}}").size() == 1,
                "Flat response remains supported");
        check(parse("{\"data\":{\"info\":[]}}").isEmpty(), "A successful empty list stays empty");
        try {
            parse("{\"data\":{\"unexpected\":[]}}");
            throw new AssertionError("Unrecognized responses must report an error");
        } catch (KuGouMusicApi.PlaylistLoadException expected) {
            check(!expected.needsLogin, "A malformed response must not invalidate the login badge");
        }
        try {
            KuGouMusicApi.userPlaylists(100);
            throw new AssertionError("An unsigned request must ask the user to log in");
        } catch (KuGouMusicApi.PlaylistLoadException expected) {
            check(expected.needsLogin, "A missing login must not look like an empty library");
        }
        System.out.println("KuGou library checks: 7 passed");
    }

    private static java.util.List<KuGouMusicApi.Playlist> parse(String json) {
        return KuGouMusicApi.parseUserPlaylists(JsonParser.parseString(json).getAsJsonObject());
    }

    private static void check(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}

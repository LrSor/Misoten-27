using UnityEngine;

namespace Sawada.Cordex
{
    //==================================================
    // 澤田さんの NetworkTestSession に「ロビー画面（GAME_LOBBY）から使う窓口」を足す
    //
    // ・NetworkTestSession は partial class（複数のファイルに分けて書ける）なので、
    //   澤田さんのファイルは変更せずに、このファイルで機能を足している
    // ・澤田さんの画面（左上の OnGUI）でやっていることを、ボタンから呼べるようにしただけ
    // ・名前がぶつからないように、全部 Suzuki で始めている
    //
    // ※ 澤田さんが中の変数名を変えるとここでコンパイルエラーになるので、
    //   そのときはこのファイルを直す（エラーの場所がすぐ分かるように、あえてこの形にしている）
    //==================================================
    public sealed partial class NetworkTestSession
    {
        // 接続中・接続済み・Host 中のどれか
        public bool SuzukiIsBusy => Busy;

        // ルーム作成・接続の途中（まだ番号がもらえていない）
        public bool SuzukiIsConnecting => Busy && localId == 0;

        // 自分の番号（1 ～ 4。未接続は 0）
        public int SuzukiLocalId => localId;

        // 今いるプレイヤーの人数
        public int SuzukiPlayerCount => states.Count;

        // 今いるプレイヤーか（id は 1 ～ 4。Host はいつも 1）
        public bool SuzukiHasPlayer(int id) => states.ContainsKey(id);

        // 澤田さんの画面に出ている状態メッセージ（英語）
        public string SuzukiStatus => status;

        // インターネットのルームコード（Host でルームの準備ができたときだけ）
        public string SuzukiRoomCode =>
            hosting && relay != null && roomAnnounced ? relay.JoinCode : "";

        // LAN のルームコード（LAN の Host のときだけ）
        public string SuzukiLanCode => hostLanCode;

        // LAN の Host の IP
        public string SuzukiHostIp =>
            hosting && listener != null && localAddresses.Length > 0
                ? localAddresses[lanAddressIndex].ToString()
                : "";

        // IP の候補が複数あるとき、次の候補にする
        public void SuzukiNextHostIp()
        {
            if (localAddresses.Length <= 1)
            {
                return;
            }

            lanAddressIndex = (lanAddressIndex + 1) % localAddresses.Length;
            UpdateHostLanCode();
        }

        // インターネットでルームを作る
        public void SuzukiCreateInternetRoom()
        {
            internetMode = true;
            StartRelayHost();
        }

        // インターネットのルームコードで参加
        public void SuzukiJoinInternetRoom(string code)
        {
            internetMode = true;
            joinCodeInput = code ?? "";
            StartRelayClient();
        }

        // LAN でルームを作る
        public void SuzukiCreateLanRoom()
        {
            internetMode = false;
            StartHost();
        }

        // LAN コード（LAN-XXXX-...）または IP（192.168.0.10 / 192.168.0.10:7777）で参加
        public void SuzukiJoinLan(string codeOrIp)
        {
            internetMode = false;

            string text = (codeOrIp ?? "").Trim();

            if (text.ToUpperInvariant().StartsWith("LAN"))
            {
                lanCodeInput = text;
                StartLanCodeClient();
                return;
            }

            int colon = text.LastIndexOf(':');

            if (colon > 0)
            {
                ip = text.Substring(0, colon);
                portText = text.Substring(colon + 1);
            }
            else
            {
                ip = text;
            }

            StartClient();
        }

        // やめる（Host を閉じる / 切断 / 接続のキャンセル）
        public void SuzukiLeave()
        {
            StopSession();
        }
    }
}

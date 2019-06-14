using System;
using System.Drawing;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Text;
using System.Net;
using System.Collections.Generic;

namespace Yutnori
{
    public enum Yut {
        DoubleBackDo = -3,
        NewBackDo = -2,
        BackDo = -1,

        Do = 1,
        Gae = 2,
        Geol = 3,
        Yoot = 4,
        Mo = 5
    }

    public partial class Form1 : Form
    {
        // 서버 클라이언트 공통 필드
        bool is_server = false;     // 현재 폼이 서버이면 true
        delegate void AppendTextDelegate(Control ctrl, string s);
        AppendTextDelegate text_appender;   // AppendText를 위한 대리자

        // 서버 측 필드
        Socket server_socket;       // 서버가 클라이언트들이 접속을 Listen하는 소켓

        // 접속한 클라이언트들을 닉네임과 mapping해서 저장
        Dictionary<string, Socket> connected_clients = new Dictionary<string, Socket>();

        string server_nickname = "Server";  // 서버의 닉네임을 저장할 변수
        string client_nickname = "Guest";   // Accept한 시점의 클라이언트의 닉네임을 저장할 변수
        int player_count = 1;       // 접속한 플레이어 수. 초기값은 서버 혼자여서 1
        int ready_count = 0;        // 준비중인 플레이어 수. 플레이어 수와 레디 수가 동일하면 게임시작
        bool p1_ready = false, p2_ready = false, p3_ready = false, p4_ready = false; //각 플레이어의 레디상태

        // 클라이언트 측 필드
        Socket client_socket;   // 클라이언트가 서버와 연결한 소켓
        string my_nickname = "Client";  // 클라이언트 자신의 닉네임
        int my_player_num = 1;     // 클라이언트 자신의 플레이어 번호 -> 클라이언트는 추후에 2,3,4 배정받을 것임
        int current_player_num = -1;    // 현재 접속한 플레이어 수 - 동기화를 위한 변수

        // 게임용 변수
        bool is_ready = false;      // 현재 자신의 레디상태
        int turn_owner = -1;        // 현재 턴을 소유중인 플레이어
        PHASE phase = PHASE.LOGIN;  // 게임의 현재 페이즈

        // 내 말 시각적 요소
        List<PictureBox> myPictureBoxList = new List<PictureBox>();

        // 저장된 윷의 리스트
        List<Yut> yuts = new List<Yut>();

        // 현재 선택된 말
        Marker selectedMarker = null;

        // 현재 선택된 말에 대해서, 목적지로 사용될 수 있는 말판의 인덱스들의 리스트.
        Dictionary<int, Yut> destDict = new Dictionary<int, Yut>();

        // 충렬0613: 말의 배열
        Marker[] markers;

        // 게임 페이즈
        enum PHASE
        {
            LOGIN,      //로그인화면
            HOSTING,    //호스트로 방 생성중
            CONNECTING, //클라이언트로 연결중
            LOBBY,      //대기실(게임 시작전 화면)
            MYTURN,     //당신의 턴
            NOT_MYTURN, //상대의 턴
            END_GAME    //게임종료
        }

        //게임 페이즈변경 함수 (인터페이스를 해당페이즈에 맞게 수정)
        void changePhase(PHASE new_phase)
        {
            if (new_phase == PHASE.LOGIN)
            {
                this.Invoke(new MethodInvoker(delegate ()
                {
                    txt_ip.ReadOnly = false;
                    txt_port.ReadOnly = false;
                    txt_nickname.ReadOnly = false;
                    btn_Host.Enabled = true;
                    btn_Client.Enabled = true;
                    btn_Roll.Enabled = false;
                    Update(); //컨트롤의 변경사항을 즉시 반영
                }));
            }
            else if (new_phase == PHASE.HOSTING)
            {
                this.Invoke(new MethodInvoker(delegate ()
                {
                    lbl_loginAlert.ForeColor = Color.Black;
                    lbl_loginAlert.Text = "생성중...";

                    txt_ip.ReadOnly = true;
                    txt_port.ReadOnly = true;
                    txt_nickname.ReadOnly = true;
                    btn_Host.Enabled = false;
                    btn_Client.Enabled = false;
                    Update(); //컨트롤의 변경사항을 즉시 반영
                }));
            }
            else if (new_phase == PHASE.CONNECTING)
            {
                this.Invoke(new MethodInvoker(delegate ()
                {
                    lbl_loginAlert.ForeColor = Color.Black;
                    lbl_loginAlert.Text = "호스트에 연결시도중...";

                    txt_ip.ReadOnly = true;
                    txt_port.ReadOnly = true;
                    txt_nickname.ReadOnly = true;
                    btn_Host.Enabled = false;
                    btn_Client.Enabled = false;
                    Update(); //컨트롤의 변경사항을 즉시 반영
                }));
            }
            else if (new_phase == PHASE.LOBBY)
            {
                this.Invoke(new MethodInvoker(delegate ()
                {
                    pnl_login.Visible = false;
                    txt_send.Enabled = true;
                    btn_send.Enabled = true;
                    btn_ready.Enabled = true;
                    btn_Roll.Enabled = false;
                    Update(); //컨트롤의 변경사항을 즉시 반영
                }));
            }
            else if (new_phase == PHASE.MYTURN)
            {
                this.Invoke(new MethodInvoker(delegate ()
                {
                    btn_Roll.Enabled = true;
                    btn_ready.Enabled = false;
                    Update();
                }));
            }
            else if (new_phase == PHASE.NOT_MYTURN)
            {
                this.Invoke(new MethodInvoker(delegate ()
                {
                    btn_Roll.Enabled = false;
                    btn_ready.Enabled = false;
                    Update();
                }));
            }
            else if (new_phase == PHASE.END_GAME)
            {
                if(is_server)
                {
                    switch(player_count)
                    {
                        case 1:
                            pb_human1.Image = img_pHuman.Images[0];
                            break;
                        case 2:
                            pb_human1.Image = img_pHuman.Images[0];
                            pb_human2.Image = img_pHuman.Images[0];
                            break;
                        case 3:
                            pb_human1.Image = img_pHuman.Images[0];
                            pb_human2.Image = img_pHuman.Images[0];
                            pb_human3.Image = img_pHuman.Images[0];
                            break;
                        case 4:
                            pb_human1.Image = img_pHuman.Images[0];
                            pb_human2.Image = img_pHuman.Images[0];
                            pb_human3.Image = img_pHuman.Images[0];
                            pb_human4.Image = img_pHuman.Images[0];
                            break;
                    }
                }
                else
                {
                    switch(current_player_num)
                    {
                        case 1:
                            pb_human1.Image = img_pHuman.Images[0];
                            break;
                        case 2:
                            pb_human1.Image = img_pHuman.Images[0];
                            pb_human2.Image = img_pHuman.Images[0];
                            break;
                        case 3:
                            pb_human1.Image = img_pHuman.Images[0];
                            pb_human2.Image = img_pHuman.Images[0];
                            pb_human3.Image = img_pHuman.Images[0];
                            break;
                        case 4:
                            pb_human1.Image = img_pHuman.Images[0];
                            pb_human2.Image = img_pHuman.Images[0];
                            pb_human3.Image = img_pHuman.Images[0];
                            pb_human4.Image = img_pHuman.Images[0];
                            break;
                    }
                }

                ready_count = 0;
                is_ready = false;

                pnl_login.Visible = false;
                txt_send.Enabled = true;
                btn_send.Enabled = true;
                btn_ready.Enabled = true;
                btn_Roll.Enabled = false;
                Update(); //컨트롤의 변경사항을 즉시 반영
            }
            phase = new_phase;
        }

        // 보조 기능 함수
        void AppendText(Control ctrl, string s)
        {// ctrl에 해당하는 컨트롤을 얻어와서 Text에 s를 삽입
            // Invoke가 필요한 컨트롤일 시 대리자를 통해 실행?
            if (ctrl.InvokeRequired) ctrl.Invoke(text_appender, ctrl, s);
            else
            {
                string source = ctrl.Text;
                ctrl.Text = source + Environment.NewLine + s;
            }
        }

        // 서버에게 닉네임을 보내줌 - 클라이언트에서 사용
        public void SendNickname()
        {
            // 서버가 대기 중인지 확인한다.
            if (!client_socket.IsBound)
            {
                MsgBoxHelper.Warn("닉네임 전송 : 서버가 실행되고 있지 않습니다!");
                return;
            }

            // 보낼 닉네임
            string nickname = my_nickname;

            // 문자열을 utf8 형식의 바이트로 변환한다.
            byte[] bDts = Encoding.UTF8.GetBytes(nickname);

            // 서버에 전송한다.
            client_socket.Send(bDts);
        }

        // 닉네임 받기 - 서버에서 사용
        void GetNickname(Socket client)
        {
            byte[] nickname_buffer = new byte[1024 * 8];

            // 서버는 클라이언트에게서 닉네임을 받고 다음 작업을 해야함.
            // -> 동기 함수인 Receive 호출
            // (서버는 클라이언트가 닉네임을보내즐 때 까지 기다린다.)
            client.Receive(nickname_buffer);

            // 서버에게 현재 클라이언트의 닉네임을 알려줌
            client_nickname = Encoding.UTF8.GetString(nickname_buffer).Trim('\0');
        }

        // 클라이언트에게 플레이어 번호를 보내줌 - 서버에서 사용
        void SendPlayerNum()
        {
            // 현재 클라이언트의 소켓을 가져온다.
            Socket socket = connected_clients[client_nickname];

            // 서버가 대기 중인지 확인한다.
            if (!socket.IsBound)
            {
                MsgBoxHelper.Warn("플레이어 번호 전송 : 서버가 실행되고 있지 않습니다!");
                return;
            }

            // 보낼 플레이어 번호
            int player_num = ++player_count;
            if (player_count > 4)   // -> 작동이 제대로 되는지 아직 모름.
            {
                MsgBoxHelper.Warn("방이 가득 찼습니다!");
                return;
            }

            // 문자열을 utf8 형식의 바이트로 변환한다.
            byte[] bDts = Encoding.UTF8.GetBytes(player_num.ToString());

            // 서버에 전송한다.
            socket.Send(bDts);
        }

        //플레이어 번호 받기 - 클라이언트에서 사용
        void GetPlayerNum()
        {
            byte[] player_num_buffer = new byte[1024 * 8];
            // 클라이언트는 서버로부터 플레이어 번호를
            // 할당 받은 후 다음 작업을 해야 함.
            // -> 동기 함수인 Receive 호출
            // (클라이언트는 서버가 플레이어 번호를 보낼 때 까지 기다림)
            client_socket.Receive(player_num_buffer);

            // 클라이언트에 서버로 부터 받은 플레이어 번호를 할당
            my_player_num = Int32.Parse(Encoding.UTF8.GetString(player_num_buffer).Trim('\0')); // error

            initializeMarkers(); // 충렬 추가
        }

        // 인자로 들어온 buffer를 모든 클라이언트에게 보낸다. - 서버가 사용
        void send_buffer_to_all_clients(byte[] buffer)
        {
            foreach (string nickname in connected_clients.Keys) {
                Socket socket = connected_clients[nickname];
                try {
                    socket.Send(buffer);
                }
                catch {
                    // 오류 발생하면 전송 취소하고 리스트에서 삭제한다.
                    try {
                        socket.Dispose();
                    }
                    catch {

                    }
                    connected_clients.Remove(nickname);
                }
            }
        }

        // 서버에게 인자로 넘긴 데이터를 전송하는 함수 - 클라이언트가 사용
        void send_data_to_server(string data)
        {
            // 데이터 형식 : "데이터종류" + '\x01' + "데이터1" + '\x01' + "데이터2" + ... + '\x01' + "데이터n"
            // 데이터 변환
            byte[] buffer = Encoding.UTF8.GetBytes(data);

            // 데이터 전송
            client_socket.Send(buffer);
        }

        // 폼 관련 함수
        public Form1()
        {
            InitializeComponent();
            // 서버, 클라이언트 소켓과 대리자 할당
            server_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            client_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            text_appender = new AppendTextDelegate(AppendText);
        }

        // 폼이 호출될 때
        private void Form1_Load(object sender, EventArgs e)
        {
            initializeMarkerArray(); // 말들의 배열을 초기화하는 부분...

            // 기본값 넣음
            txt_ip.Text = "127.0.0.1";
            txt_port.Text = "7777";
        }

        // 폼이 닫힐 때(미구현)
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //if(!is_server)
            //    ServerStop();  //서버 중지
            //else
            //    Disconnect();  //연결 중지
        }

        // 생성 버튼이 눌릴 때. 즉 서버가 만들어질 때.
        private void btn_Host_Click(object sender, EventArgs e)
        {
            int port;
            // 포트 번호 예외처리
            if (!int.TryParse(txt_port.Text, out port)) {
                lbl_loginAlert.ForeColor = Color.Red;
                lbl_loginAlert.Text = "잘못된 포트번호입니다.";
                txt_port.Focus();
                txt_port.SelectAll();
                return;
            }
            if (txt_nickname.Text == "") {
                lbl_loginAlert.ForeColor = Color.Red;
                lbl_loginAlert.Text = "잘못된 닉네임입니다.";
                txt_nickname.Focus();
                txt_nickname.SelectAll();
                return;
            }

            changePhase(PHASE.HOSTING);

            is_server = true; // 생성 버튼을 눌렀으니 이 프로세스는 서버다.
            server_nickname = txt_nickname.Text;// 서버의 닉네임 가져옴.

            // 텍스트 박스에 서버(1P)의 접속을 써준다.
            AppendText(rtb_chat, string.Format("<System>{0}(1P)님이 입장하였습니다.", server_nickname));

            // 서버에서 클라이언트의 연결 요청을 대기하기 위해
            // 소켓을 열어둔다.
            IPEndPoint server_ep = new IPEndPoint(IPAddress.Any, port);
            server_socket.Bind(server_ep);
            server_socket.Listen(3);    // 최대 3명의 클라이언트를 받는다.

            // 비동기적으로 클라이언트의 연결 요청을 받는다.
            // 연결 요청이 들어왔을 때 콜백인 AcceptCallback 함수가
            // 자동으로 호출 됨.
            server_socket.BeginAccept(AcceptCallback, null);

            // 게임인터페이스로 이동
            changePhase(PHASE.LOBBY);

            pb_chat.Image = img_pNumber.Images[0];
            pb_human1.Image = img_pHuman.Images[0];
            ready_count = 0;
            is_ready = false;

            initializeMarkers(); // 말 초기화 (충렬 추가)
        }

        // 클라이언트가 "입장" 눌렀을 때
        private void btn_Client_Click(object sender, EventArgs e)
        {
            if (client_socket.Connected)
            {
                MsgBoxHelper.Error("이미 연결되어 있습니다!");
                return;
            }

            int port;
            if (!int.TryParse(txt_port.Text, out port))
            {
                lbl_loginAlert.ForeColor = Color.Red;
                lbl_loginAlert.Text = "잘못된 포트번호입니다.";
                txt_port.Focus();
                txt_port.SelectAll();
                return;
            }
            if (txt_nickname.Text == "")
            {
                lbl_loginAlert.ForeColor = Color.Red;
                lbl_loginAlert.Text = "잘못된 닉네임입니다.";
                txt_nickname.Focus();
                txt_nickname.SelectAll();
                return;
            }

            changePhase(PHASE.CONNECTING);

            try
            {
                client_socket.Connect(txt_ip.Text, port);
            }
            catch (Exception ex)
            {
                lbl_loginAlert.ForeColor = Color.Red;
                lbl_loginAlert.Text = "연결에 실패했습니다!";
                changePhase(PHASE.LOGIN);
                return;
            }

            // 연결 완료되었다는 메세지를 띄워준다.
            //AppendText(rtb_chat, "서버와 연결되었습니다.");

            // 클라이언트 자신의 닉네임을 설정한다.
            my_nickname = txt_nickname.Text;

            // 서버에게 닉네임을 보낸다.
            SendNickname();

            // 서버에게서 플레이어 번호를 할당받는다.
            GetPlayerNum();

            // 채팅창 플레이어 이미지를 설정한다
            pb_chat.Image = img_pNumber.Images[my_player_num - 1];

            //게임인터페이스로 이동
            changePhase(PHASE.LOBBY);    // 로비로 이동.(준비 전 상태)

            // 채팅창을 사용 가능하도록 변경
            txt_send.Enabled = true;
            btn_send.Enabled = true;
            btn_ready.Enabled = true;

            // 연결 완료, 서버에서 데이터가 올 수 있으므로 수신 대기한다.
            AsyncObject obj = new AsyncObject(1024 * 8);
            // 데이터 송신을 대기할 소켓 지정
            obj.WorkingSocket = client_socket;
            // 해당 소켓이 비동기로 데이터를 받는 함수 BeginReceive 호출
            client_socket.BeginReceive(obj.Buffer, 0, obj.BufferSize, 0, DataReceived, obj);
        }

        // 채팅 보내기
        void chat_send()
        {
            if (is_server)  // 함수를 호출한 프로세스가 서버일 경우
            {
                // 서버가 대기 중인지 확인한다.
                if (!server_socket.IsBound) {
                    MsgBoxHelper.Warn("서버가 실행되고 있지 않습니다!");
                    return;
                }

                // 보낼 텍스트 지정
                string tts = txt_send.Text.Trim();

                // 보낼 텍스트가 비어 있지 않다면
                if (!string.IsNullOrEmpty(tts)) {
                    // 서버의 닉네임과 메세지 및 플레이어 번호(1)를 전송한다.
                    // 문자열을 utf8 형식의 바이트로 변환한다. ('\x01'은 구분자.)
                    byte[] bDts = Encoding.UTF8.GetBytes("chat" + '\x01' + server_nickname + '\x01' + tts + '\x01' + "1");

                    // 연결된 모든 클라이언트에게 전송한다.
                    send_buffer_to_all_clients(bDts);

                    // 전송 완료 후 텍스트 박스에 추가하고, 원래의 내용은 지운다.
                    AppendText(rtb_chat, string.Format("(1P){0}: {1}", server_nickname.ToString(), tts));
                    txt_send.Clear();

                    // 커서를 텍스트박스로
                    txt_send.Focus();
                }
            }
            else  // 함수를 호출한 프로세스가 클라이언트이면
            {
                // 서버가 대기 중인지 확인한다.
                if (!client_socket.IsBound) {
                    MsgBoxHelper.Warn("서버가 실행되고 있지 않습니다!");
                    return;
                }

                // 보낼 텍스트 지정
                string tts = txt_send.Text.Trim();


                // 보낼 텍스트가 비어 있지 않으면
                if (!string.IsNullOrEmpty(tts)) {
                    // 클라이언트의 닉네임과 메세지를 전송한다.
                    // 문자열을 utf8 형식의 바이트로 변환한다. ('\x01'은 구분자)
                    string data = "chat" + '\x01' + my_nickname + '\x01' + tts + '\x01' + my_player_num.ToString();

                    //서버에 전송한다
                    send_data_to_server(data);
                    // 전송 완료 후 텍스트 박스에 추가하고, 원래의 내용은 지운다.
                    //AppendText(rtb_chat, string.Format("({0}P){1}: {2}", my_player_num, my_nickname, tts));
                    txt_send.Clear();

                    // 커서를 텍스트박스로
                    txt_send.Focus();
                }
            }
        }

        // "Send" 버튼을 눌렀을 때
        private void btn_send_Click(object sender, EventArgs e)
        {// 데이터 전송 - 서버, 클라이언트 둘 다 사용
            chat_send(); // 메시지 전송
        }

        // 메시지 text box에서 엔터를 눌렀을 때의 event handler
        private void txt_send_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) // 엔터가 눌리면
                chat_send(); // 메시지 전송
        }

        // 클라이언트의 접속 요청이 Accept 되었을 때 호출되는 콜백함수
        void AcceptCallback(IAsyncResult ar)
        {
            // 클라이언트의 연결 요청을 수락한다.
            Socket client = server_socket.EndAccept(ar);

            // 또 다른 클라이언트의 연결을 대기한다.
            server_socket.BeginAccept(AcceptCallback, null);

            // 비동기 통신을 위한 AsyncObject 객체 생성 및 소켓 지정
            AsyncObject obj = new AsyncObject(1024 * 8);
            obj.WorkingSocket = client;

            // 클라이언트의 닉네임을 얻는다.
            GetNickname(client);

            // 연결된 클라이언트 딕셔너리에 추가해준다.
            connected_clients.Add(client_nickname, client);

            // 클라이언트에게 플레이어 번호를 할당해준다.
            SendPlayerNum();

            // 현재 접속한 플레이어 수에 따라 이미지를 동기화 해준다.
            switch (player_count) {
                case 2:
                    pb_human2.Image = img_pHuman.Images[0];
                    break;
                case 3:
                    pb_human3.Image = img_pHuman.Images[0];
                    break;
                case 4:
                    pb_human4.Image = img_pHuman.Images[0];
                    break;
            }

            // 모든 클라이언트에게 마지막 클라이언트가 연결되었다고 써준다.
            // 문자열을 utf8 형식의 바이트로 변환한다.
            // 현재 접속한 플레이어 수와 클라이언트 연결 메세지를 보낸다.
            string tts = string.Format("<System>{0}({1}P)님이 입장하였습니다.", client_nickname, player_count);
            byte[] bDts = Encoding.UTF8.GetBytes("login" + '\x01' + player_count.ToString() + '\x01' + tts
                 + '\x01' + p1_ready + '\x01' + p2_ready + '\x01' + p3_ready + '\x01' + p4_ready);

            // 연결된 모든 클라이언트에게 전송한다.
            send_buffer_to_all_clients(bDts);
            
            // 전송 완료 후 텍스트 박스에 추가한다.
            AppendText(rtb_chat, string.Format("<System>{0}({1}P)님이 입장하였습니다.", client_nickname, player_count));

            // 클라이언트의 데이터를 받는다.
            client.BeginReceive(obj.Buffer, 0, 1024 * 8, 0, DataReceived, obj);
        }

        // 데이터 받기 - 서버, 클라이언트 둘 다 사용
        void DataReceived(IAsyncResult ar)
        {
            // BeginReceive에서 추가적으로 넘어온 데이터를 Asyncobject 형식으로 변환한다.
            AsyncObject obj = (AsyncObject)ar.AsyncState;

            // 데이터 수신을 끝낸다.
            try {
                int received = obj.WorkingSocket.EndReceive(ar); // 클라 끄면 서버 꺼지는 이유
            }
            catch (Exception e) {
                MessageBox.Show(e.Message);
                obj.WorkingSocket.Close();
                this.Close();
                return;
            }

            // 텍스트로 변환한다.
            string text = Encoding.UTF8.GetString(obj.Buffer);

            // 0x01 기준으로 자른다. == '\x01' 기준으로 자른다.
            string[] tokens = text.Split('\x01');


            // 첫 번째 토큰 확인 후 알맞은 처리
            // 마지막 토큰에는 .Trim('\0')을 붙여줘서 데이터 뒤의 널문자를 제거해준다.
            switch (tokens[0]) {
                // (1) 채팅 관련
                case "chat": {
                        // 서버
                        if (is_server) {
                            string nickname = tokens[1];
                            string msg = tokens[2];
                            string player_num = tokens[3].Trim('\0');

                            // 텍스트 박스에 추가해준다.
                            // (비동기식으로 작업하기 때문에 폼이 UI 스레드에서 작업을 해줘야 한다.
                            //  따라서 대리자를 통해 처리한다.(Invoke))
                            AppendText(rtb_chat, string.Format("({0}P){1}: {2}", player_num, nickname, msg));

                            // 모든 클라이언트에게 전송
                            send_buffer_to_all_clients(obj.Buffer);
                        }

                        // 클라이언트
                        else {
                            string nickname = tokens[1];
                            string msg = tokens[2];
                            string player_num = tokens[3].Trim('\0');

                            // 텍스트 박스에 추가해준다.
                            // (비동기식으로 작업하기 때문에 폼이 UI 스레드에서 작업을 해줘야 한다.
                            //  따라서 대리자를 통해 처리한다.(Invoke))
                            AppendText(rtb_chat, string.Format("({0}P){1}: {2}", player_num, nickname, msg));
                        }

                        break;
                    }

                // (2) 준비 상태 관련
                case "ready": {
                        // 서버
                        if (is_server) {
                            string readied_player = tokens[1];
                            bool ready_state = bool.Parse(tokens[2].Trim('\0'));
                            switch (readied_player) {
                                case "2":
                                    if (ready_state) {
                                        p2_ready = true;
                                        pb_human2.Image = img_pHuman.Images[2];
                                        ready_count++;
                                    }
                                    else {
                                        p2_ready = false;
                                        pb_human2.Image = img_pHuman.Images[0];
                                        ready_count--;
                                    }
                                    break;
                                case "3":
                                    if (ready_state) {
                                        p3_ready = true;
                                        pb_human3.Image = img_pHuman.Images[3];
                                        ready_count++;
                                    }
                                    else {
                                        p3_ready = false;
                                        pb_human3.Image = img_pHuman.Images[0];
                                        ready_count--;
                                    }
                                    break;
                                case "4":
                                    if (ready_state) {
                                        p4_ready = true;
                                        pb_human4.Image = img_pHuman.Images[4];
                                        ready_count++;
                                    }
                                    else {
                                        p4_ready = false;
                                        pb_human4.Image = img_pHuman.Images[0];
                                        ready_count--;
                                    }
                                    break;
                            }

                            // 모든 클라이언트에게 전송
                            send_buffer_to_all_clients(obj.Buffer);

                            //게임시작을 시도한다. (게임시작의 여부는 해당 함수내에서 판별)
                            GameStart();
                        }

                        // 클라이언트
                        else {
                            string readied_player = tokens[1];
                            bool ready_state = bool.Parse(tokens[2].Trim('\0'));
                            switch (readied_player) {
                                case "1":
                                    if (ready_state) {
                                        p1_ready = true;
                                        pb_human1.Image = img_pHuman.Images[1];
                                        ready_count++;
                                    }
                                    else {
                                        p1_ready = false;
                                        pb_human1.Image = img_pHuman.Images[0];
                                        ready_count--;
                                    }
                                    break;
                                case "2":
                                    if (ready_state) {
                                        p2_ready = true;
                                        pb_human2.Image = img_pHuman.Images[2];
                                        ready_count++;
                                    }
                                    else {
                                        p2_ready = false;
                                        pb_human2.Image = img_pHuman.Images[0];
                                        ready_count--;
                                    }
                                    break;
                                case "3":
                                    if (ready_state) {
                                        p3_ready = true;
                                        pb_human3.Image = img_pHuman.Images[3];
                                        ready_count++;
                                    }
                                    else {
                                        p3_ready = false;
                                        pb_human3.Image = img_pHuman.Images[0];
                                        ready_count--;
                                    }
                                    break;
                                case "4":
                                    if (ready_state) {
                                        p4_ready = true;
                                        pb_human4.Image = img_pHuman.Images[4];
                                        ready_count++;
                                    }
                                    else {
                                        p4_ready = false;
                                        pb_human4.Image = img_pHuman.Images[0];
                                        ready_count--;
                                    }
                                    break;
                            }

                            //게임시작을 시도한다. (게임시작의 여부는 해당 함수내에서 판별)
                            GameStart();
                        }

                        break;
                    }

                // (3) 차례 변경 요청
                case "update_turn": {
                        // 서버
                        if (is_server) {
                            this.turn_owner = Int32.Parse(tokens[1].Trim('\0'));
                            if (turn_owner == 1)
                                changePhase(PHASE.MYTURN);
                            else
                                changePhase(PHASE.NOT_MYTURN);
                            // 모든 클라이언트에게 전송
                            send_buffer_to_all_clients(obj.Buffer);
                        }

                        // 클라이언트
                        else {
                            this.turn_owner = Int32.Parse(tokens[1].Trim('\0'));
                            if (turn_owner == my_player_num)
                                changePhase(PHASE.MYTURN);
                            else
                                changePhase(PHASE.NOT_MYTURN);
                        }

                        break;
                    }

                // (4) 로그인 관련(client only)
                case "login": {
                        current_player_num = Int32.Parse(tokens[1]);
                        string login_msg = tokens[2].Trim('\0');

                        //서버로부터 각 플레이어들의 레디상태를 받는다.
                        bool p1_ready = bool.Parse(tokens[3]);
                        bool p2_ready = bool.Parse(tokens[4]);
                        bool p3_ready = bool.Parse(tokens[5]);
                        bool p4_ready = bool.Parse(tokens[6].Trim('\0'));

                        // 텍스트 박스에 추가해준다.
                        // (비동기식으로 작업하기 때문에 폼이 UI 스레드에서 작업을 해줘야 한다.
                        // 따라서 대리자를 통해 처리한다.(Invoke))
                        AppendText(rtb_chat, login_msg);

                        // 플레이어의 접속 이미지를 동기화 한다.
                        switch (current_player_num) {
                            case 2:
                                pb_human1.Image = img_pHuman.Images[0];
                                pb_human2.Image = img_pHuman.Images[0];
                                break;
                            case 3:
                                pb_human1.Image = img_pHuman.Images[0];
                                pb_human2.Image = img_pHuman.Images[0];
                                pb_human3.Image = img_pHuman.Images[0];
                                break;
                            case 4:
                                pb_human1.Image = img_pHuman.Images[0];
                                pb_human2.Image = img_pHuman.Images[0];
                                pb_human3.Image = img_pHuman.Images[0];
                                pb_human4.Image = img_pHuman.Images[0];
                                break;
                        }
                        if (p1_ready)
                        {
                            if(my_player_num == current_player_num) ready_count++;
                            pb_human1.Image = img_pHuman.Images[1];
                        }
                        if (p2_ready)
                        {
                            if (my_player_num == current_player_num) ready_count++;
                            pb_human2.Image = img_pHuman.Images[2];
                        }
                        if (p3_ready)
                        {
                            if (my_player_num == current_player_num) ready_count++;
                            pb_human3.Image = img_pHuman.Images[3];
                        }
                        if (p4_ready)
                        {
                            if (my_player_num == current_player_num) ready_count++;
                            pb_human4.Image = img_pHuman.Images[4];
                        }
                        break;
                    }

                // (5) 시스템 메시지를 받으면, 출력(client only)
                case "system_msg": {
                        string system_msg = tokens[1].Trim('\0');

                        // 텍스트 박스에 추가해준다.
                        // (비동기식으로 작업하기 때문에 폼이 UI 스레드에서 작업을 해줘야 한다.
                        // 따라서 대리자를 통해 처리한다.(Invoke))
                        AppendText(rtb_chat, system_msg);

                        break;
                    }

                // (7) 말 정보 받는 부분?
                case "marker": {
                    if (is_server) {
                        send_buffer_to_all_clients(obj.Buffer);     // 받은거 그대로 보냄.

                        // 서버에 저장
                        for(int i=0; i<16; i++) {
                            markers[i].playerNum = int.Parse(tokens[i * 3 + 1]);
                            markers[i].index = int.Parse(tokens[i * 3 + 2]);

                            if (i == 15) {
                                markers[i].position = int.Parse(tokens[i * 3 + 3].Trim('\0'));
                            }
                            else {
                                markers[i].position = int.Parse(tokens[i * 3 + 3]);
                            }
                        }

                        // 화면 업데이트
                        UpdateAll();
                    }
                    else {
                        // 서버에 저장
                        for (int i = 0; i < 16; i++) {
                            markers[i].playerNum = int.Parse(tokens[i * 3 + 1]);
                            markers[i].index = int.Parse(tokens[i * 3 + 2]);

                            if (i == 15) {
                                markers[i].position = int.Parse(tokens[i * 3 + 3].Trim('\0'));
                            }
                            else {
                                markers[i].position = int.Parse(tokens[i * 3 + 3]);
                            }
                        }

                        // 화면 업데이트
                        UpdateAll();
                    }
                    break;
                }

                // (8) 게임이 끝난 경우
                case "end_game": {
                        string win_player = tokens[1].Trim('\0');

                        string msg = "<System> " + win_player.ToString() + "이 승리했습니다!";
                        string data = "system_msg" + '\x01' + msg;
                        AppendText(rtb_chat, msg);
                        changePhase(PHASE.END_GAME);

                        if (is_server)
                        {
                            byte[] buffer = Encoding.UTF8.GetBytes(data);
                            send_buffer_to_all_clients(buffer);
                        }
                        break;
                    }
            }

            // 데이터를 받은 후엔 다시 버퍼를 비워주고
            obj.ClearBuffer();

            // 같은 방법으로 수신을 대기한다.
            obj.WorkingSocket.BeginReceive(obj.Buffer, 0, 1024 * 8, 0, DataReceived, obj);
        }

        // 레디를 누르면 일단 해당 함수가 시작된다.
        void GameStart()
        {
            // 플레이어수가 1명이거나 모두 준비를 하지 않은 경우에는 리턴
            if ((phase != PHASE.LOBBY) ||
                (is_server && (ready_count <= 1 || ready_count < player_count)) ||
                (!is_server && ready_count < current_player_num))
            {
                MessageBox.Show("not yet");
                return;
            }

            if (is_server) {
                string tts = string.Format("<System>게임이 시작되었습니다.");
                byte[] bDts = Encoding.UTF8.GetBytes("system_msg" + '\x01' + tts);
                send_buffer_to_all_clients(bDts);
                AppendText(rtb_chat, string.Format("<System>게임이 시작되었습니다."));
            }
            
            turn_owner = 1; //현재 턴은 1P의 것이다.
            changePhase(PHASE.NOT_MYTURN);   //자신 페이즈를 '자신의 턴이 아닌 상태'로 변경

            if(turn_owner == my_player_num) //만약 현재 턴의 소유자가 본인이라면
                changePhase(PHASE.MYTURN);   //자신의 페이즈를 '자신의 턴'으로 변경
            
        }

        // 도움말 버튼을 눌렀을 때 이벤트 핸들러
        private void btn_help_Click(object sender, EventArgs e)
        {
            Form2 child = new Form2();
            child.Show();
        }

        // 말의 배열을 초기화하는 함수(Load에서 호출)
        private void initializeMarkerArray()
        {
            markers = new Marker[16];

            markers[0] = new Marker(1, 1, pbMarker1p_1);
            markers[1] = new Marker(1, 2, pbMarker1p_2);
            markers[2] = new Marker(1, 3, pbMarker1p_3);
            markers[3] = new Marker(1, 4, pbMarker1p_4);

            markers[4] = new Marker(2, 1, pbMarker2p_1);
            markers[5] = new Marker(2, 2, pbMarker2p_2);
            markers[6] = new Marker(2, 3, pbMarker2p_3);
            markers[7] = new Marker(2, 4, pbMarker2p_4);

            markers[8] = new Marker(3, 1, pbMarker3p_1);
            markers[9] = new Marker(3, 2, pbMarker3p_2);
            markers[10] = new Marker(3, 3, pbMarker3p_3);
            markers[11] = new Marker(3, 4, pbMarker3p_4);

            markers[12] = new Marker(4, 1, pbMarker4p_1);
            markers[13] = new Marker(4, 2, pbMarker4p_2);
            markers[14] = new Marker(4, 3, pbMarker4p_3);
            markers[15] = new Marker(4, 4, pbMarker4p_4);
        }

        // 반복문 돌려서 화면을 다 업데이트 하는 부분
        private void UpdateAll()
        {
            // 모든 말에 대해서 반복
            for (int i = 0; i < 16; i++) {
                // 말이 골인 했으면 숨기자
                if (markers[i].isFinished || markers[i].position == 1000) {
                    Invoke((MethodInvoker)(() => {
                        markers[i].pb.Visible = false;
                    }));
                }

                // 말이 골인 안했으면
                else {

                    // 시작 지점에 있는 말이 아닌 경우 -> 보이게 하자
                    if (markers[i].position != 0) {
                        Point point = markers[i].getPoint(markers[i].position); // 몇 번째 칸에 있는지 받아서 좌표를 반환
                        Invoke((MethodInvoker)(() => {
                            markers[i].pb.Location = point;
                            markers[i].pb.Visible = true;
                        }));
                    }

                    // 시작 지점에 있는 말인 경우
                    else
                    {
                        // 자기 말이 아니다
                        if (markers[i].playerNum != this.my_player_num) {
                            Invoke((MethodInvoker)(() => {
                                markers[i].pb.Visible = false;
                            }));
                        }

                        // 자기 말이다
                        else {
                            Point point = new Point();
                            switch (markers[i].index) {
                                case 1:
                                    point = new Point(396, 209);
                                    break;
                                case 2:
                                    point = new Point(396, 240);
                                    break;
                                case 3:
                                    point = new Point(396, 271);
                                    break;
                                case 4:
                                    point = new Point(396, 302);
                                    break;
                            }

                            markers[i].pb.Invoke((MethodInvoker)(() => {
                                markers[i].pb.Location = point;
                            }));
                        }
                    }
                }
            }
        }

        // 말의 이미지 초기화하는 함수
        private void initializeMarkers()
        {
            // 일단 말을 다 숨김.
            for (int i = 0; i < 16; i++) {
                markers[i].pb.Visible = false;
            }

            // 자기 말은 다 표시
            for (int i = 0; i < 16; i++) {
                if (markers[i].playerNum == this.my_player_num) {
                    markers[i].pb.Visible = true;
                }
            }

            // 자기 말의 위치 다 초기 위치로 이동.
            for (int i = 0; i < 16; i++) {
                if (markers[i].playerNum == this.my_player_num) {
                    if (markers[i].index == 1) {
                        markers[i].pb.Location = new Point(396, 209);
                    }

                    if (markers[i].index == 2) {
                        markers[i].pb.Location = new Point(396, 240);
                    }

                    if (markers[i].index == 3) {
                        markers[i].pb.Location = new Point(396, 271);
                    }

                    if (markers[i].index == 4) {
                        markers[i].pb.Location = new Point(396, 302);
                    }

                    markers[i].pb.Click += selectMarker;
                    markers[i].pb.MouseMove += mousemoveMarker;
                    markers[i].pb.MouseLeave += mouseleaveMarker;
                }
            }
        }

        // 말에다가 마우스를 올리면
        private void mousemoveMarker(object sender, MouseEventArgs e)
        {
            PictureBox current_marker = sender as PictureBox;
            if (selectedMarker != null && current_marker == selectedMarker.pb)
                current_marker.Image = img_character.Images[7 + my_player_num];
            else if (yuts.Count >= 1)
                current_marker.Image = img_character.Images[3 + my_player_num];
        }

        // 말에서 마우스가 나가면
        private void mouseleaveMarker(object sender, EventArgs e)
        {
            PictureBox current_marker = sender as PictureBox;
            if (selectedMarker != null && current_marker == selectedMarker.pb)
                current_marker.Image = img_character.Images[7 + my_player_num];
            else if (yuts.Count >= 1)
                current_marker.Image = img_character.Images[my_player_num - 1];
        }

        // 말판에서 사용하는 공통 이벤트 핸들러
        private void stepClick(object sender, EventArgs e)
        {
            bool isCatch = false;

            if(selectedMarker != null) {
                // pb to int
                int marker_index = PictureBoxToInt((PictureBox)sender);
                int prev_position = selectedMarker.position;

                // 유효한 이동인 경우에만 코드 작동 시킨다.
                if (destDict.ContainsKey(marker_index)) {

                    for(int i = 0; i < markers.Length; i++)
                    {
                        if(SimplifyIndex(markers[i].position) == SimplifyIndex(marker_index))
                        {
                            if(markers[i].playerNum != my_player_num)
                            {
                                // 잡기
                                markers[i].position = 0;

                                // 한번 더 던지기
                                isCatch = true;
                            }
                        }
                    }

                    // dict에서 뺀다.
                    yuts.Remove(destDict[marker_index]);
                    destDict.Remove(marker_index);

                    // 지름길 처리
                    if (marker_index == 5) marker_index = 105;
                    if (marker_index == 10) marker_index = 210;
                    if (marker_index == 108) marker_index = 213;

                    // 위치 변경
                    selectedMarker.position = marker_index;

                    if (prev_position != 0)
                    {
                        for (int i = 0; i < markers.Length; i++)
                        {
                            if (SimplifyIndex(markers[i].position) == SimplifyIndex(prev_position))
                            {
                                if (markers[i].playerNum == my_player_num)
                                {
                                    // 업기
                                    markers[i].position = marker_index;
                                }
                            }
                        }
                    }

                    sendMarkersData();
                    UpdateAll();

                    selectedMarker.pb.Image = img_character.Images[my_player_num - 1];
                    selectedMarker = null;

                    if (marker_index == 1000)
                        Step_1000.Visible = false;


                    // 몇개 finish?
                    int finMarkerCount = 0;

                    foreach (Marker marker in markers) {
                        if ((marker.playerNum == my_player_num) && (marker.position == 1000)) {
                            finMarkerCount++;
                        }
                    }

                    // 4개를 다 골인한 경우 == 게임이 끝난 경우 승리한 플레이어의 정보를 보내준다.
                    if (finMarkerCount == 4) {
                        // 승리한 플레이어 정보 보내기
                        string data = "end_game" + '\x01' + my_player_num.ToString();

                        if (is_server) {
                            byte[] buffer = Encoding.UTF8.GetBytes(data);
                            send_buffer_to_all_clients(buffer);

                            string msg = "<System> " + my_player_num.ToString() + "이 승리했습니다!";
                            AppendText(rtb_chat, msg);
                            changePhase(PHASE.END_GAME);
                        }
                        else {
                            
                            send_data_to_server(data);
                        }
                    }

                    // 이동을 완료했다. 혹시 yuts가 비어 있으면 턴을 다음으로 넘긴다.
                    else if (yuts.Count == 0 && !isCatch)
                    {
                        nextTurn();
                    }

                    // 이동 완료 안함 (리스트에 윷이 남아 있거나 잡은 경우 한 번 더 던지도록.) - 수정(?)
                    else if(yuts.Count == 0 && isCatch)
                    {
                        btn_Roll.Enabled = true;
                    }
                }
            }
        }

        // 말을 선택했을 때의 이벤트 핸들러
        private void selectMarker(object sender, EventArgs e)
        {
            Step_1000.Visible = false; // 나갈수있는거 안나가고 다른거클릭하면 없어짐

            if (yuts.Count <= 0)
                return;

            for (int i = 0; i < 15; i++) {
                if (sender == markers[i].pb) {
                    selectedMarker = markers[i];
                    selectedMarker.pb.Image = img_character.Images[7 + my_player_num];
                }
                else {
                    markers[i].pb.Image = img_character.Images[i / 4];
                }
            }

            // 선택 이후, 내가 갈 수 있는 목적지의 인덱스들을 리스트에 추가한다.
            destDict.Clear();

            foreach (Yut y in yuts) {
                int dest_index = selectedMarker.position + (int)y;
                dest_index = SimplifyIndex(dest_index);

                if (!destDict.ContainsKey(dest_index))
                    destDict.Add(dest_index, y);

                if (dest_index == 1000)
                    Step_1000.Visible = true;

                // 빽도 예외 처리
                if (y == Yut.BackDo)
                {
                    switch (SimplifyIndex(selectedMarker.position))
                    {
                        case 1:
                            destDict.Add(20, y);
                            break;
                        case 15:
                            destDict.Add(110, y);
                            break;
                        case 20:
                            destDict.Add(215, y);
                            break;
                        case 108:
                            destDict.Add(212, y);
                            break;
                    }
                }
            }
        }

        // 인덱스 값을 바꾸는 부분
        private int SimplifyIndex(int idx)
        {
            // 나가기
            if ((20 < idx && idx <=25) ||
                (116 < idx && idx <= 121) ||
                (216 < idx && idx <= 221))
                return 1000;


            switch (idx) {
                case 105:
                    return 5;
                case 210:
                    return 10;
                case 111:
                    return 15;
                case 112:
                    return 16;
                case 113:
                    return 17;
                case 114:
                    return 18;
                case 115:
                    return 19;
                case 116:
                case 216:
                    return 20;
                case 213:
                    return 108;
                default:
                    return idx;
            }
        }

        private int PictureBoxToInt(PictureBox pb)
        {
            if (pb == Step_1) return 1;
            if (pb == Step_2) return 2;
            if (pb == Step_3) return 3;
            if (pb == Step_4) return 4;
            if (pb == Step_5) return 5;
            if (pb == Step_6) return 6;
            if (pb == Step_7) return 7;
            if (pb == Step_8) return 8;
            if (pb == Step_9) return 9;
            if (pb == Step_10) return 10;
            if (pb == Step_11) return 11;
            if (pb == Step_12) return 12;
            if (pb == Step_13) return 13;
            if (pb == Step_14) return 14;
            if (pb == Step_15) return 15;
            if (pb == Step_16) return 16;
            if (pb == Step_17) return 17;
            if (pb == Step_18) return 18;
            if (pb == Step_19) return 19;
            if (pb == Step_20) return 20;
            if (pb == Step_106) return 106;
            if (pb == Step_107) return 107;
            if (pb == Step_108) return 108;
            if (pb == Step_109) return 109;
            if (pb == Step_110) return 110;
            if (pb == Step_211) return 211;
            if (pb == Step_212) return 212;
            if (pb == Step_214) return 214;
            if (pb == Step_215) return 215;
            if (pb == Step_1000) return 1000;

            return -1;
        }
        
        
        // 던지기를 누른다.
        private void btn_Roll_Click(object sender, EventArgs e)
        {
            Random rand = new Random();
            //int number = rand.Next(1000);
            int number = rand.Next(0, 1000);

            //테스트코드 - 도:130, 개:350, 걸:650, 윷:900, 모:950, 빽도:30, 빨빽:65, 두빽도:260
            if (yuts.Count == 0) number = 950;
            if (yuts.Count == 1) number = 950;
            if (yuts.Count == 2) number = 950;
            if (yuts.Count == 3) number = 950;

            if (yuts.Count == 4) number = 950;
            if (yuts.Count == 5) number = 950;
            if (yuts.Count == 6) number = 350;

            // 도
            if (120 <= number && number < 250) // 도 1
            {
                // 이미지를 변경한다.
                if (120 <= number && number < 152.5)
                {
                    pb_yut1.Image = Properties.Resources.yut_back0;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut4.Image = Properties.Resources.yut_front;

                }
                else if (152.5 <= number && number < 185)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_back0;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut4.Image = Properties.Resources.yut_front;
                }
                else if (185 <= number && number < 217.5)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_back0;
                    pb_yut4.Image = Properties.Resources.yut_front;
                }
                else if (217.5 <= number && number < 250)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut4.Image = Properties.Resources.yut_back0;
                }

                yuts.Add(Yut.Do);
            }
            
            else if (0 <= number && number < 62) // 빽도 -1
            {
                if (0 <= number && number < 15)
                {
                    pb_yut1.Image = Properties.Resources.yut_back1;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut4.Image = Properties.Resources.yut_front;
                }
                else if (15 <= number && number < 30)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_back1;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut4.Image = Properties.Resources.yut_front;
                }
                else if (30 <= number && number < 45)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_back1;
                    pb_yut4.Image = Properties.Resources.yut_front;
                }
                else if (45 <= number && number < 62)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut4.Image = Properties.Resources.yut_back1;
                }

                // 내 말 중 어떠한 말도 배치되지 않은 경우, 턴을 넘긴다.
                // isPlaced: 하나라도 배치가 되었는가?
                bool isPlaced = false;
                for (int i = 0; i < 15; i++)
                {
                    if (markers[i].playerNum == this.my_player_num)
                    {
                        if (markers[i].position != 0 && markers[i].position != 1000)
                            isPlaced = true;
                        if (yuts.Count > 0)
                            isPlaced = true;
                    }
                }

                if (isPlaced)
                {
                    yuts.Add(Yut.BackDo);
                }
                else
                {
                    nextTurn();
                }
            }
            else if (62 <= number && number < 120) // 빨빽 -2
            {
                if (62 <= number && number < 75)
                {
                    pb_yut1.Image = Properties.Resources.yut_back2;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut4.Image = Properties.Resources.yut_front;
                }
                else if (75 <= number && number < 90)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_back2;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut4.Image = Properties.Resources.yut_front;
                }
                else if (90 <= number && number < 105)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_back2;
                    pb_yut4.Image = Properties.Resources.yut_front;
                }
                else if (105 <= number && number < 120)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut4.Image = Properties.Resources.yut_back2;
                }

                // 내 말 중 어떠한 말도 배치되지 않은 경우, 턴을 넘긴다.
                // isPlaced: 하나라도 배치가 되었는가?
                bool isPlaced = false;
                for (int i = 0; i < 15; i++)
                {
                    if (markers[i].playerNum == this.my_player_num)
                    {
                        if ((markers[i].position != 0) && markers[i].position != 1000)
                            isPlaced = true;
                    }
                }

                if (isPlaced)
                {
                    yuts.Add(Yut.BackDo);
                }
                else
                {
                    nextTurn();
                }
            }
            else if (312 <= number && number < 625) // 개 2
            {//52
                if (312 <= number && number < 364)
                {
                    pb_yut1.Image = Properties.Resources.yut_back0;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_back0;
                    pb_yut4.Image = Properties.Resources.yut_front;
                }
                else if (364 <= number && number < 416)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_back0;
                    pb_yut4.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_back0;
                }
                else if (416 <= number && number < 468)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_back0;
                    pb_yut4.Image = Properties.Resources.yut_back0;

                }
                else if (468 <= number && number < 520)
                {
                    pb_yut1.Image = Properties.Resources.yut_back0;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_back0;
                    pb_yut4.Image = Properties.Resources.yut_front;

                }
                else if (520 <= number && number < 572)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_back0;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut4.Image = Properties.Resources.yut_back0;

                }
                else if (572 <= number && number < 625)
                {
                    pb_yut1.Image = Properties.Resources.yut_back0;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut4.Image = Properties.Resources.yut_back0;

                }
                yuts.Add(Yut.Gae);
            }
            else if (250 <= number && number < 312) //두개빽 -3
            {
                if (250 <= number && number < 270)
                {
                    pb_yut1.Image = Properties.Resources.yut_back1;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut4.Image = Properties.Resources.yut_back2;
                }
                else if (270 <= number && number < 290)
                {
                    pb_yut4.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_back2;
                    pb_yut2.Image = Properties.Resources.yut_back1;
                    pb_yut1.Image = Properties.Resources.yut_front;
                }
                else if (290 <= number && number < 312)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_back1;
                    pb_yut4.Image = Properties.Resources.yut_back2;
                }
                yuts.Add(Yut.DoubleBackDo);
            }
            else if (625 <= number && number < 875) // 걸 3
            { // 62.5
                if (625 <= number && number < 687.5)
                {
                    pb_yut1.Image = Properties.Resources.yut_back0;
                    pb_yut2.Image = Properties.Resources.yut_back0;
                    pb_yut3.Image = Properties.Resources.yut_back0;
                    pb_yut4.Image = Properties.Resources.yut_front;
                }
                else if (687.5 <= number && number < 750)
                {
                    pb_yut1.Image = Properties.Resources.yut_back0;
                    pb_yut2.Image = Properties.Resources.yut_back0;
                    pb_yut3.Image = Properties.Resources.yut_front;
                    pb_yut4.Image = Properties.Resources.yut_back0;
                }
                else if (750 <= number && number < 812.5)
                {
                    pb_yut1.Image = Properties.Resources.yut_back0;
                    pb_yut2.Image = Properties.Resources.yut_front;
                    pb_yut3.Image = Properties.Resources.yut_back0;
                    pb_yut4.Image = Properties.Resources.yut_back0;
                }
                else if (812.5 <= number && number < 875)
                {
                    pb_yut1.Image = Properties.Resources.yut_front;
                    pb_yut2.Image = Properties.Resources.yut_back0;
                    pb_yut3.Image = Properties.Resources.yut_back0;
                    pb_yut4.Image = Properties.Resources.yut_back0;
                }

                yuts.Add(Yut.Geol);
            }
            else if (875 <= number && number < 940) // 윷 4
            {
                pb_yut1.Image = Properties.Resources.yut_back0;
                pb_yut2.Image = Properties.Resources.yut_back0;
                pb_yut3.Image = Properties.Resources.yut_back0;
                pb_yut4.Image = Properties.Resources.yut_back0;

                yuts.Add(Yut.Yoot);
            }
            else if (940 <= number) //모 5
            {
                pb_yut1.Image = Properties.Resources.yut_front;
                pb_yut2.Image = Properties.Resources.yut_front;
                pb_yut3.Image = Properties.Resources.yut_front;
                pb_yut4.Image = Properties.Resources.yut_front;

                yuts.Add(Yut.Mo);
            }

            // 윷이나 모가 아니면 버튼을끈다.
            if(yuts.Count != 0)
            {
                if (yuts[yuts.Count - 1] != Yut.Mo && yuts[yuts.Count - 1] != Yut.Yoot)
                {
                    changePhase(PHASE.NOT_MYTURN);
                }
            }
            else
            {
                changePhase(PHASE.NOT_MYTURN);
            }
        }

        // 채팅창이 바뀔 때 event handler -> 스크롤 맨 아래로 내린다.
        private void rtb_chat_TextChanged(object sender, EventArgs e)
        {
            rtb_chat.SelectionStart = rtb_chat.Text.Length;
            rtb_chat.ScrollToCaret();
        }

        // 준비 버튼 눌렀을 때 event handler -> 플레이어 이미지에 색깔이 입혀진다.
        private void btn_ready_Click(object sender, EventArgs e)
        {
            //자신의 레디상태를 변화시킨다.
            is_ready = !is_ready;
            string data = "ready" + '\x01' + my_player_num + '\x01' + is_ready;

            if (is_server)  // 서버가 준비 버튼을 눌렀을 때
            {
                byte[] buffer = Encoding.UTF8.GetBytes(data);
                send_buffer_to_all_clients(buffer);

                // 서버 자신의 변수상태를 변경시킨다.
                if (is_ready)
                {
                    p1_ready = true;
                    pb_human1.Image = img_pHuman.Images[1];
                    ready_count++;
                }
                else
                {
                    p1_ready = false;
                    pb_human1.Image = img_pHuman.Images[0];
                    ready_count--;
                }

                //게임시작을 시도한다. (게임시작의 여부는 해당 함수내에서 판별)
                GameStart();
            }
            else    // 클라이언트가 준비 버튼을 눌렀을 때
            {

                send_data_to_server(data);
            }
        }

        // 이충렬0613 : 턴을 넘기는 함수
        private void nextTurn()
        {
            if (is_server)
            {
                if (this.turn_owner != player_count)
                    this.turn_owner++;
                else
                    this.turn_owner = 1;

                string data = "update_turn" + '\x01' + turn_owner.ToString();

                byte[] buffer = Encoding.UTF8.GetBytes(data);
                send_buffer_to_all_clients(buffer);
                changePhase(PHASE.NOT_MYTURN);
            }
            else
            {
                if (this.turn_owner != current_player_num)
                    this.turn_owner++;
                else
                    this.turn_owner = 1;

                string data = "update_turn" + '\x01' + turn_owner.ToString();

                send_data_to_server(data);
            }
        }

        //충렬0613: 모든 말 정보를 서버로 보냅니다.
        private void sendMarkersData()
        {
            // 무엇을 보낼지 스트링을 만들어 나가는 작업
            string make_string = "marker";
            for (int i = 0; i < 16; i++) {
                make_string += "\x01" + markers[i].playerNum.ToString() + "\x01" + markers[i].index.ToString() + "\x01" + markers[i].position.ToString();
            }

            // 서버인 경우 바이트 배열을 전송
            if (is_server) {
                byte[] buffer = Encoding.UTF8.GetBytes(make_string);
                send_buffer_to_all_clients(buffer);
            }

            // 클라이언트인 경우 스트링을 그대로 전송
            else {
                send_data_to_server(make_string);
            }
        }
    }
}

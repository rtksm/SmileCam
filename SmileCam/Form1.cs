using System;
using System.Management;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SmileAutoCam
{
    public partial class Form1 : Form
    {
        private int _command;
        private Byte[] _dataHeader;
        private int _dataLength;
        private int _readLength;
        private Byte[] _dataBody;

        const int RESP_HEADER_LENGTH = 6;

        /****************************************************************************/
        /*!
		 *	@brief	ボーレート格納用のクラス定義.
		 */
        private class BuadRateItem : Object
        {
            private string m_name = "";
            private int m_value = 0;

            // 表示名称
            public string NAME
            {
                set { m_name = value; }
                get { return m_name; }
            }

            // ボーレート設定値.
            public int BAUDRATE
            {
                set { m_value = value; }
                get { return m_value; }
            }

            // コンボボックス表示用の文字列取得関数.
            public override string ToString()
            {
                return m_name;
            }
        }

        /****************************************************************************/
        /*!
		 *	@brief	制御プロトコル格納用のクラス定義.
		 */
        private class HandShakeItem : Object
        {
            private string m_name = "";
            private Handshake m_value = Handshake.None;

            // 表示名称
            public string NAME
            {
                set { m_name = value; }
                get { return m_name; }
            }

            // 制御プロトコル設定値.
            public Handshake HANDSHAKE
            {
                set { m_value = value; }
                get { return m_value; }
            }

            // コンボボックス表示用の文字列取得関数.
            public override string ToString()
            {
                return m_name;
            }
        }

        private delegate void Delegate_RcvData(Byte[] header, int bodyLength, Byte[] body);

        /****************************************************************************/
        /*!
		 *	@brief	コンストラクタ.
		 *
		 *	@param	なし.
		 *
		 *	@retval	なし.
		 */
        public Form1()
        {
            InitializeComponent();
        }

        /****************************************************************************/
        /*!
		 *	@brief	ダイアログの初期処理.
		 *
		 *	@param	[in]	sender	イベントの送信元のオブジェクト.
		 *	@param	[in]	e		イベント情報.
		 *
		 *	@retval	なし.
		 */
        private void Form1_Load(object sender, EventArgs e)
        {
            //! 利用可能なシリアルポート名の配列を取得する.
            string[] PortList = SerialPort.GetPortNames();

            cmbPortName.Items.Clear();

            //! シリアルポート名をコンボボックスにセットする.
            foreach (string PortName in PortList)
            {
                cmbPortName.Items.Add(PortName);
            }
            if (cmbPortName.Items.Count > 0)
            {
                cmbPortName.SelectedIndex = 0;
            }

            cmbBaudRate.Items.Clear();

            // ボーレート選択コンボボックスに選択項目をセットする.
            BuadRateItem baud;
            baud = new BuadRateItem();
            baud.NAME = "4800bps";
            baud.BAUDRATE = 4800;
            cmbBaudRate.Items.Add(baud);

            baud = new BuadRateItem();
            baud.NAME = "9600bps";
            baud.BAUDRATE = 9600;
            cmbBaudRate.Items.Add(baud);

            baud = new BuadRateItem();
            baud.NAME = "38400bps";
            baud.BAUDRATE = 38400;
            cmbBaudRate.Items.Add(baud);

            baud = new BuadRateItem();
            baud.NAME = "115200bps";
            baud.BAUDRATE = 115200;
            cmbBaudRate.Items.Add(baud);

            baud = new BuadRateItem();
            baud.NAME = "230400bps";
            baud.BAUDRATE = 115200;
            cmbBaudRate.Items.Add(baud);

            baud = new BuadRateItem();
            baud.NAME = "460800bps";
            baud.BAUDRATE = 115200;
            cmbBaudRate.Items.Add(baud);

            baud = new BuadRateItem();
            baud.NAME = "921600bps";
            baud.BAUDRATE = 115200;
            cmbBaudRate.Items.Add(baud);
            cmbBaudRate.SelectedIndex = cmbBaudRate.Items.Count - 1;

            cmbHandShake.Items.Clear();

            // フロー制御選択コンボボックスに選択項目をセットする.
            HandShakeItem ctrl;
            ctrl = new HandShakeItem();
            ctrl.NAME = "なし";
            ctrl.HANDSHAKE = Handshake.None;
            cmbHandShake.Items.Add(ctrl);

            ctrl = new HandShakeItem();
            ctrl.NAME = "XON/XOFF制御";
            ctrl.HANDSHAKE = Handshake.XOnXOff;
            cmbHandShake.Items.Add(ctrl);

            ctrl = new HandShakeItem();
            ctrl.NAME = "RTS/CTS制御";
            ctrl.HANDSHAKE = Handshake.RequestToSend;
            cmbHandShake.Items.Add(ctrl);

            ctrl = new HandShakeItem();
            ctrl.NAME = "XON/XOFF + RTS/CTS制御";
            ctrl.HANDSHAKE = Handshake.RequestToSendXOnXOff;
            cmbHandShake.Items.Add(ctrl);
            cmbHandShake.SelectedIndex = 0;

            // 送受信用のテキストボックスをクリアする.
            rcvTextBox.Clear();
        }

        /****************************************************************************/
        /*!
		 *	@brief	ダイアログの終了処理.
		 *
		 *	@param	[in]	sender	イベントの送信元のオブジェクト.
		 *	@param	[in]	e		イベント情報.
		 *
		 *	@retval	なし.
		 */
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            //! シリアルポートをオープンしている場合、クローズする.
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
            }
        }

        /****************************************************************************/
        /*!
		 *	@brief	[終了]ボタンを押したときの処理.
		 *
		 *	@param	[in]	sender	イベントの送信元のオブジェクト.
		 *	@param	[in]	e		イベント情報.
		 *
		 *	@retval	なし.
		 */
        private void exitButton_Click(object sender, EventArgs e)
        {
            //! ダイアログをクローズする.
            Close();
        }

        /****************************************************************************/
        /*!
		 *	@brief	[接続]/[切断]ボタンを押したときにシリアルポートのオープン/クローズを行う.
		 *
		 *	@param	[in]	sender	イベントの送信元のオブジェクト.
		 *	@param	[in]	e		イベント情報.
		 *
		 *	@retval	なし.
		 */
        private void connectButton_Click(object sender, EventArgs e)
        {

            if (serialPort1.IsOpen == true)
            {
                //! シリアルポートをクローズする.
                serialPort1.Close();

                //! ボタンの表示を[切断]から[接続]に変える.
                connectButton.Text = "接続";
            }
            else
            {
                //! オープンするシリアルポートをコンボボックスから取り出す.
                serialPort1.PortName = cmbPortName.SelectedItem.ToString();

                //! ボーレートをコンボボックスから取り出す.
                BuadRateItem baud = (BuadRateItem)cmbBaudRate.SelectedItem;
                serialPort1.BaudRate = baud.BAUDRATE;

                //! データビットをセットする. (データビット = 8ビット)
                serialPort1.DataBits = 8;

                //! パリティビットをセットする. (パリティビット = なし)
                serialPort1.Parity = Parity.None;

                //! ストップビットをセットする. (ストップビット = 1ビット)
                serialPort1.StopBits = StopBits.One;

                //! フロー制御をコンボボックスから取り出す.
                HandShakeItem ctrl = (HandShakeItem)cmbHandShake.SelectedItem;
                serialPort1.Handshake = ctrl.HANDSHAKE;

                //! 文字コードをセットする.
                serialPort1.Encoding = Encoding.Unicode;

                //! 読み込みタイムアウト時間をセットする．
                //serialPort1.ReadTimeout = 10000;

                try
                {
                    //! シリアルポートをオープンする.
                    serialPort1.Open();

                    //! ボタンの表示を[接続]から[切断]に変える.
                    connectButton.Text = "切断";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        /****************************************************************************/
        /*!
		 *	@brief	[送信]ボタンを押して、データ送信を行う.
		 *
		 *	@param	[in]	sender	イベントの送信元のオブジェクト.
		 *	@param	[in]	e		イベント情報.
		 *
		 *	@retval	なし.
		 */
        private void sndButton_Click(object sender, EventArgs e)
        {
            //! シリアルポートをオープンしていない場合、処理を行わない.
            if (serialPort1.IsOpen == false)
            {
                return;
            }
            //! 送信するテキスト
            Byte[] btArray = null;
            _command = int.Parse(tabControl1.SelectedTab.Text.Substring(0,2), System.Globalization.NumberStyles.HexNumber);

            switch (_command)
            {
                case 0:
                    btArray = new Byte[4] { 0xFE, 0x00, 0x00, 0x00 };
                    break;
                case 1:
                    btArray = new Byte[5] { 0xFE, 0x01, 0x01, 0x00, 0x00 };
                    break;
                case 2:
                    btArray = new Byte[4] { 0xFE, 0x02, 0x00, 0x00 };
                    break;
                case 4:
                    btArray = new Byte[7] { 0xFE, 0x04, 0x03, 0x00, 0x00, 0x00, 0x00 };
                    btArray[4] = (byte)((checkBox1.Checked == true ? 0x80 : 0) +
                                        (checkBox2.Checked == true ? 0x40 : 0) +
                                        (checkBox3.Checked == true ? 0x20 : 0) +
                                        (checkBox4.Checked == true ? 0x10 : 0) +
                                        (checkBox5.Checked == true ? 0x08 : 0) +
                                        (checkBox6.Checked == true ? 0x04 : 0) +
                                        (checkBox7.Checked == true ? 0x02 : 0) +
                                        (checkBox8.Checked == true ? 0x01 : 0));
                    btArray[5] = (byte)((checkBox9.Checked == true ? 0x02 : 0) +
                                        (checkBox10.Checked == true ? 0x01 : 0));
                    btArray[6] = (byte)((radioButton2.Checked == true ? 0x01 :
                                        (radioButton3.Checked == true ? 0x02 : 0x00)));
                    break;
                default:
                    return;
            }

            //! 送信するテキストがない場合、データ送信は行わない.
            if (btArray == null || btArray.Length<= 0)
            {
                return;
            }

            try
            {
                //! シリアルポートからテキストを送信する.
                serialPort1.Write(btArray,0,btArray.Length);

                //! 2重送信を防止する．
                sndButton.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /****************************************************************************/
        /*!
		 *	@brief	データ受信が発生したときのイベント処理.
		 *
		 *	@param	[in]	sender	イベントの送信元のオブジェクト.
		 *	@param	[in]	e		イベント情報.
		 *
		 *	@retval	なし.
		 */
        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            Debug.WriteLine("serialPort1_DataReceived");
            int iSize = 0;
            //! シリアルポートをオープンしていない場合、処理を行わない.
            if (serialPort1.IsOpen == false)
            {
                return;
            }

            try
            {
                if (_dataLength == 0)
                {
                    if (serialPort1.BytesToRead < RESP_HEADER_LENGTH)
                    {
                        return;
                    }

                    //! ヘッダ部を読み込む.
                    _dataHeader = new Byte[RESP_HEADER_LENGTH];
                    serialPort1.Read(_dataHeader, 0, RESP_HEADER_LENGTH);
                    if (_dataHeader[0] == 0xFE)
                    {
                        _dataLength = _dataHeader[2];
                        _dataLength += _dataHeader[3] << 8;
                        _dataLength += _dataHeader[4] << 16;
                        _dataLength += _dataHeader[5] << 24;

                        Debug.WriteLine(_dataLength.ToString() + " ");

                        _dataBody = new Byte[_dataLength];
                        _readLength = 0;
                    }
                }
                else
                {
                    iSize = serialPort1.BytesToRead;
                    Debug.WriteLine(iSize.ToString() + " ");
                    if (iSize > _dataLength - _readLength) iSize = _dataLength - _readLength;
                    serialPort1.Read(_dataBody, _readLength, iSize);
                    _readLength += iSize;
                }
                Debug.WriteLine(_readLength.ToString() + " ");
                if (_readLength == _dataLength)
                {
                    //! 受信したデータをメインスレッドに送る.
                    Invoke(new Delegate_RcvData(RcvData), new Object[] { _dataHeader, _dataLength, _dataBody });
                    _dataHeader = null;
                    _dataLength = 0;
                    _readLength = 0;
                    _dataBody = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            if (serialPort1.BytesToRead > 0) serialPort1_DataReceived(sender, e);
        }

        /****************************************************************************/
        /*!
		 *	@brief	受信データをテキストボックスに書き込む.
		 *
		 *	@param	[in]	data	受信した文字列.
		 *
		 *	@retval	なし.
		 */
        private void RcvData(Byte[] header, int bodyLength, Byte[] body)
        {
            rcvTextBox.Clear();

            ////! 受信データをテキストボックスの最後に追記する.
            //for (int i = 0; i < RESP_HEADER_LENGTH; i++)
            //{
            //    rcvTextBox.AppendText(header[i].ToString("X2") + " ");
            //}
            //for (int i = 0; i < bodyLength; i++)
            //{
            //    rcvTextBox.AppendText(body[i].ToString("X2") + " ");
            //}
            //rcvTextBox.AppendText("\n");

            rcvTextBox.AppendText("コマンド=" + _command.ToString("X2") + "\n");
            rcvTextBox.AppendText("レスポンスコード=" + header[1].ToString("X2") + "\n");
            rcvTextBox.AppendText("データ長=" + _dataLength.ToString() + "\n");

            switch (_command)
            {
                case 0:
                    rcvTextBox.AppendText("形式文字列=" + System.Text.Encoding.GetEncoding("shift-jis").GetString(body, 0, 12) + "\n");
                    rcvTextBox.AppendText("メジャーバージョン=" + body[12].ToString() + "\n");
                    rcvTextBox.AppendText("マイナーバージョン=" + body[13].ToString() + "\n");
                    rcvTextBox.AppendText("リリースバージョン=" + body[14].ToString() + "\n");
                    rcvTextBox.AppendText("リビジョン番号=" + ((int)(body[15]) + (int)(body[16])*256 + (int)(body[17])*256*256 +(int)(body[18])*256*256*256).ToString() + "\n");
                    break;
                case 2:
                    rcvTextBox.AppendText("カメラ取付方向=" + body[0].ToString() + "\n");
                    break;
                case 4:
                    rcvTextBox.AppendText("人体検出数=" + body[0].ToString() + "\n");
                    rcvTextBox.AppendText("手検出数=" + body[1].ToString() + "\n");
                    rcvTextBox.AppendText("顔検出数=" + body[2].ToString() + "\n");
                    rcvTextBox.AppendText("予約=" + body[3].ToString() + "\n");

                    int iPos = 4;
                    int iCnt = body[0];
                    if( iCnt > 0 )
                    {
                        for(int i=0; i<iCnt; i++)
                        {
                            rcvTextBox.AppendText("人体[" + i.ToString() + "]\n");
                            rcvTextBox.AppendText("座標X=" + (body[iPos++] + body[iPos++] * 256).ToString() + "\n");
                            rcvTextBox.AppendText("座標Y=" + (body[iPos++] + body[iPos++] * 256).ToString() + "\n");
                            rcvTextBox.AppendText("検出サイズ=" + (body[iPos++] + body[iPos++] * 256).ToString() + "\n");
                            rcvTextBox.AppendText("信頼度=" + (body[iPos++] + body[iPos++] * 256).ToString() + "\n");
                       }
                    }
                    iCnt = body[1];
                    if (iCnt > 0)
                    {
                        for (int i = 0; i < iCnt; i++)
                        {
                            rcvTextBox.AppendText("手[" + i.ToString() + "]\n");
                            rcvTextBox.AppendText("座標X=" + (body[iPos++] + body[iPos++] * 256).ToString() + "\n");
                            rcvTextBox.AppendText("座標Y=" + (body[iPos++] + body[iPos++] * 256).ToString() + "\n");
                            rcvTextBox.AppendText("検出サイズ=" + (body[iPos++] + body[iPos++] * 256).ToString() + "\n");
                            rcvTextBox.AppendText("信頼度=" + (body[iPos++] + body[iPos++] * 256).ToString() + "\n");
                        }
                    }
                    iCnt = body[2];
                    if (iCnt > 0)
                    {
                        for (int i = 0; i < iCnt; i++)
                        {
                            if (checkBox6.Checked == true)
                            {
                                rcvTextBox.AppendText("顔[" + i.ToString() + "]\n");
                                rcvTextBox.AppendText("座標X=" + (body[iPos++] + body[iPos++] * 256).ToString() + "\n");
                                rcvTextBox.AppendText("座標Y=" + (body[iPos++] + body[iPos++] * 256).ToString() + "\n");
                                rcvTextBox.AppendText("検出サイズ=" + (body[iPos++] + body[iPos++] * 256).ToString() + "\n");
                                rcvTextBox.AppendText("信頼度=" + (body[iPos++] + body[iPos++] * 256).ToString() + "\n");
                            }
                            if (checkBox5.Checked == true)
                            {
                                rcvTextBox.AppendText("顔向き推定[" + i.ToString() + "]\n");
                                rcvTextBox.AppendText("左右方向角度=" + ((Int16)(body[iPos++] + body[iPos++] * 256)).ToString() + "\n");
                                rcvTextBox.AppendText("上下方向角度=" + ((Int16)(body[iPos++] + body[iPos++] * 256)).ToString() + "\n");
                                rcvTextBox.AppendText("顔傾き角度=" + ((Int16)(body[iPos++] + body[iPos++] * 256)).ToString() + "\n");
                                rcvTextBox.AppendText("信頼度=" + (body[iPos++] + body[iPos++] * 256).ToString() + "\n");
                            }
                            if (checkBox4.Checked == true)
                            {
                                rcvTextBox.AppendText("年齢推定[" + i.ToString() + "]\n");
                                rcvTextBox.AppendText("年齢=" + body[iPos++].ToString() + "\n");
                                rcvTextBox.AppendText("信頼度=" + ((Int16)(body[iPos++] + body[iPos++] * 256)).ToString() + "\n");
                            }
                            if (checkBox3.Checked == true)
                            {
                                rcvTextBox.AppendText("性別推定[" + i.ToString() + "]\n");
                                rcvTextBox.AppendText("性別=" + body[iPos++].ToString() + "\n");
                                rcvTextBox.AppendText("信頼度=" + ((Int16)(body[iPos++] + body[iPos++] * 256)).ToString() + "\n");
                            }
                            if (checkBox2.Checked == true)
                            {
                                rcvTextBox.AppendText("視線推定[" + i.ToString() + "]\n");
                                rcvTextBox.AppendText("左右角度=" + body[iPos++].ToString() + "\n");
                                rcvTextBox.AppendText("上下角度=" + body[iPos++].ToString() + "\n");
                            }
                            if (checkBox1.Checked == true)
                            {
                                rcvTextBox.AppendText("目つむり推定[" + i.ToString() + "]\n");
                                rcvTextBox.AppendText("目つむり度合い(左)=" + ((Int16)(body[iPos++] + body[iPos++] * 256)).ToString() + "\n");
                                rcvTextBox.AppendText("目つむり度合い(右)=" + ((Int16)(body[iPos++] + body[iPos++] * 256)).ToString() + "\n");
                            }
                            if (checkBox10.Checked == true)
                            {
                                rcvTextBox.AppendText("表情推定[" + i.ToString() + "]\n");
                                rcvTextBox.AppendText("無表情=" + body[iPos++].ToString() + "\n");
                                rcvTextBox.AppendText("喜び=" + body[iPos++].ToString() + "\n");
                                rcvTextBox.AppendText("驚き=" + body[iPos++].ToString() + "\n");
                                rcvTextBox.AppendText("怒り=" + body[iPos++].ToString() + "\n");
                                rcvTextBox.AppendText("悲しみ=" + body[iPos++].ToString() + "\n");
                                rcvTextBox.AppendText("ネガ/ポジ=" + body[iPos++].ToString() + "\n");
                            }
                            if (checkBox9.Checked == true)
                            {
                                rcvTextBox.AppendText("顔認証[" + i.ToString() + "]\n");
                                rcvTextBox.AppendText("ユーザID=" + ((Int16)(body[iPos++] + body[iPos++] * 256)).ToString() + "\n");
                                rcvTextBox.AppendText("スコア=" + ((Int16)(body[iPos++] + body[iPos++] * 256)).ToString() + "\n");
                            }
                        }
                        if (radioButton2.Checked == true || radioButton3.Checked == true)
                        {
                            rcvTextBox.AppendText("画像データ\n");
                            int w = (body[iPos++] + body[iPos++] * 256);
                            rcvTextBox.AppendText("幅=" + w.ToString() + "\n");
                            int h = (body[iPos++] + body[iPos++] * 256);
                            rcvTextBox.AppendText("高さ=" + h.ToString() + "\n");
                            Byte[] dat = new Byte[w * h * 3];
                            for (int i = 0; i < h; i++)
                            {
                                for (int j = 0; j < w; j++)
                                {
                                    if (iPos >= body.Length) break;
                                    int index = i * w * 3 + j * 3;
                                    dat[index] = body[iPos];
                                    dat[index + 1] = body[iPos];
                                    dat[index + 2] = body[iPos++];
                                }
                            }
                            using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb))
                            {
                                BitmapData bd = bmp.LockBits(
                                    new Rectangle(0, 0, w, h),
                                    ImageLockMode.WriteOnly,
                                    PixelFormat.Format24bppRgb);

                                Marshal.Copy(dat, 0, bd.Scan0, dat.Length);

                                bmp.UnlockBits(bd);

                                pictureBox1.Width = w;
                                pictureBox1.Height = h;
                                pictureBox1.Refresh();
                                pictureBox1.CreateGraphics().DrawImage(bmp, 0, 0);
                            }
                        }
                    }
                    break;
                default:
                    break;
            }
            sndButton.Enabled = true;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using EnsembleSlave.Bluetooth;
using System.Runtime.InteropServices;

namespace EnsembleSlave
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        DispatcherTimer playTimer;
        DispatcherTimer ensembleTimer;
        List<int> timeList = new List<int>();
        List<byte[]> freqsList = new List<byte[]>();
        byte[] currentFreqs;
        int listIndex = 0;
        MidiManager midi = new MidiManager();

        public bool IsConnectRealSense = false;
        PXCMSenseManager senseManager;
        PXCMProjection projection;
        PXCMCapture.Device device;
        PXCMHandModule handAnalyzer;
        PXCMHandData handData;
        PXCMHandConfiguration config;

        private BluetoothWindow bluetoothWindow;
        public DateTime Target;
        public DateTime ntpBasicTime = new DateTime(1900, 1, 1);
        public System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        
        public MainWindow()
        {
            InitializeComponent();

            Top = Constants.TopMargin;
            Left = 0;
            InstrumentsList.ItemsSource = Instruments.JNames;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadChordProgress("kanon.csv");
            InitEnsembleTimer();
            InitializeRealSense();
            OpenBluetoothWindow();
            
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            rsw.Start();
            lsw.Start();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (bluetoothWindow != null) bluetoothWindow.Close();
            Uninitialize();
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (Constants.RealSenseIsConnect) UpdateRealSense();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            StartEnsemble();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopEnsemble();
        }

        private void BluetoothButton_Click(object sender, RoutedEventArgs e)
        {
            OpenBluetoothWindow();
        }

        /// <summary> 終了処理 </summary>
        private void Uninitialize()
        {
            if (senseManager != null)
            {
                senseManager.Dispose();
                senseManager = null;
            }
            if (projection != null)
            {
                projection.Dispose();
                projection = null;
            }
            if (handData != null)
            {
                handData.Dispose();
                handData = null;
            }

            if (handAnalyzer != null)
            {
                handAnalyzer.Dispose();
                handAnalyzer = null;
            }
            if (config != null)
            {
                config.UnsubscribeGesture(OnFiredGesture);
                config.Dispose();
            }
        }

        #region Midi関連メソッド

        private void LoadChordProgress(string filename)
        {
            try
            {
                // csvファイルを開く
                using (var sr = new System.IO.StreamReader(Constants.RSOURCES_PATH + filename))
                {
                    // ストリームの末尾まで繰り返す
                    while (!sr.EndOfStream)
                    {
                        // ファイルから一行読み込む
                        var line = sr.ReadLine();
                        // 読み込んだ一行をカンマ毎に分けて配列に格納する
                        var values = line.Split(',');
                        timeList.Add(int.Parse(values[0]));
                        byte[] freqs = new byte[values.Length - 1];
                        for (int i = 1; i < values.Length; i++)
                        {
                            freqs[i - 1] = byte.Parse(values[i]);
                        }
                        freqsList.Add(freqs);
                    }
                }
            }
            catch (Exception e)
            {
                // ファイルを開くのに失敗したとき
                Console.WriteLine("failed to open the file : " + e.Message);
            }
        }
        
        public async void SetTarget(string getSignal)
        {
            Console.WriteLine("receive signal in SetTarget : " + getSignal);
            string[] tokens = getSignal.Split(':');
            string targetStr = tokens[0] + ":" + tokens[1] + ":" + tokens[2] + ":" + tokens[3];
            string format = "";
            for (int i = 0; i < tokens[0].Length; i++) format += "H";
            format += ":";
            for (int i = 0; i < tokens[1].Length; i++) format += "m";
            format += ":";
            for (int i = 0; i < tokens[2].Length; i++) format += "s";
            format += ":";
            for (int i = 0; i < tokens[3].Length; i++) format += "f";
            Target = DateTime.ParseExact(targetStr, format, null);
            await Task.Run(() => WaitForStart());
            
            //InitPlayTimer();
            //Console.WriteLine(Target.ToLongDateString());
        }

        private void WaitForStart()
        {
            int waitTime = (int)(Target - ntpBasicTime.Add(sw.Elapsed)).TotalMilliseconds;
            if (waitTime > 0)
            {
                System.Threading.Thread.Sleep(waitTime);
            }
            StartEnsemble();
            Console.WriteLine("waitTime"+waitTime);
        }

        private void InitPlayTimer()
        {
            //初期化、普通にする際はプロパティはNormalでよいかと
            playTimer = new DispatcherTimer(DispatcherPriority.Normal);
            //左から　日数、時間、分、秒、ミリ秒で設定　今回は10ミリ秒ごとつまり1秒あたり100回処理します
            playTimer.Interval = new TimeSpan(0, 0, 0, 0, 10);
            //dispatcherTimer.Tick += new EventHandler(DispatcherTimer_Tick);
            playTimer.Tick += new EventHandler(PlayTimer_Tick);
            playTimer.Start();
        }

        private void PlayTimer_Tick(object sender, EventArgs e)
        {
            DateTime now = ntpBasicTime.Add(sw.Elapsed);
            if (now > Target)
            {
                StartEnsemble();
                playTimer.Stop();
                Console.WriteLine("target in Playtimer_tick : " + Target.Hour + ":" + Target.Minute + ":" + Target.Second + ":" + Target.Millisecond);
                Console.WriteLine("now in Playtimer_tick : " + now.Hour + ":" + now.Minute + ":" + now.Second + ":" + now.Millisecond);
            }
        }

        private void InitEnsembleTimer()
        {
            //初期化、普通にする際はプロパティはNormalでよいかと
            ensembleTimer = new DispatcherTimer(DispatcherPriority.Normal);
            //左から　日数、時間、分、秒、ミリ秒で設定　今回は10ミリ秒ごとつまり1秒あたり100回処理します
            ensembleTimer.Interval = new TimeSpan(0, 0, 0, 2, 0);
            //dispatcherTimer.Tick += new EventHandler(DispatcherTimer_Tick);
            //ensembleTimer.Tick += new EventHandler(EnsembleTimer_Tick);
        }

        private void EnsembleTimer_Tick(object sender, EventArgs e)
        {
            if (listIndex >= timeList.Count)
            {
                ensembleTime.Stop();
                StopEnsemble();
                //Console.WriteLine("ensembleTime");
                //Console.WriteLine(ensembleTime.Elapsed);
                //Console.WriteLine(ensembleTime.ElapsedMilliseconds);
                //listIndex = 0;
                if (RepeatCheck.IsChecked == false)
                {
                    //ensembleTimer.Stop();
                    //PlayButton.IsEnabled = true;
                    return;
                }
            }
            UpdateChord();
            //int sec = DateTime.Now.Second;
            //int mill = DateTime.Now.Millisecond;
            //DateTime now = ntpStartTime.Add(sw.Elapsed);
            //Console.WriteLine("local update:" + sec + mill);
            //Console.WriteLine("ntp update:" + now.Second + ":" + now.Millisecond);
        }

        public System.Diagnostics.Stopwatch ensembleTime = new System.Diagnostics.Stopwatch();

        public async void StartEnsemble()
        {
            //UpdateChord();
            ensembleTime.Start();
            ensembleTimer.Start();
            await Task.Run(() => UpdateEnsemble());
            

            int min = DateTime.Now.Minute;
            int sec = DateTime.Now.Second;
            int mill = DateTime.Now.Millisecond;

            DateTime now = ntpBasicTime.Add(sw.Elapsed);
            Console.WriteLine("target : " + Target.Minute + ":" + Target.Second + ":" + Target.Millisecond);
            Console.WriteLine("pc start time in StartEnsemble : " + min + ":" + sec + ":" + mill);
            Console.WriteLine("ntp start time in StartEnsemble : " + now.Minute + ":" + now.Second + ":" + now.Millisecond);

            //PlayButton.IsEnabled = false;
        }

        private void UpdateEnsemble()
        {
            for(int i = 1; i < freqsList.Count+1; i++)
            {
                UpdateChord();
                int waitTime = (int)(Target.AddMilliseconds(2000 * i) - ntpBasicTime.Add(sw.Elapsed)).TotalMilliseconds;
                System.Threading.Thread.Sleep(waitTime);
            }
            ensembleTime.Stop();
            ensembleTimer.Stop();
        }

        public void StopEnsemble()
        {
            ensembleTimer.Stop();
            int sec = DateTime.Now.Second;
            int mill = DateTime.Now.Millisecond;
            DateTime now = ntpBasicTime.Add(sw.Elapsed);
            //Console.WriteLine("local stop:"+sec+mill);
            //Console.WriteLine("ntp stop:" + now.Second + ":" + now.Millisecond);
            listIndex = 0;
            PlayButton.IsEnabled = true;
        }

        private void UpdateChord()
        {
            /*
            string freqs = "Chord Progress: ";
            foreach (byte freq in freqsList[listIndex])
            {
                freqs += freq.ToString() + " ";
            }
            //Chord.Text = freqs;
            */
            //Console.WriteLine("list index in UpdateChord :" + listIndex);
            Console.WriteLine("ensembletime in UpdateChord :" + ensembleTime.ElapsedMilliseconds);
            currentFreqs = freqsList[listIndex];
            listIndex++;
        }

        #endregion

        #region RealSense

        /// <summary> 機能の初期化 </summary>
        private void InitializeRealSense()
        {
            try
            {
                //SenseManagerを生成
                senseManager = PXCMSenseManager.CreateInstance();

                //カラーストリームの有効
                var sts = senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR,
                    Constants.COLOR_WIDTH, Constants.COLOR_HEIGHT, Constants.COLOR_FPS);
                if (sts < pxcmStatus.PXCM_STATUS_NO_ERROR)
                {
                    throw new Exception("Colorストリームの有効化に失敗しました");
                }

                // Depthストリームを有効にする
                sts = senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_DEPTH,
                    Constants.DEPTH_WIDTH, Constants.DEPTH_HEIGHT, Constants.DEPTH_FPS);
                if (sts < pxcmStatus.PXCM_STATUS_NO_ERROR)
                {
                    throw new Exception("Depthストリームの有効化に失敗しました");
                }

                // 手の検出を有効にする
                sts = senseManager.EnableHand();
                if (sts < pxcmStatus.PXCM_STATUS_NO_ERROR)
                {
                    throw new Exception("手の検出の有効化に失敗しました");
                }

                //パイプラインを初期化する
                //(インスタンスはInit()が正常終了した後作成されるので，機能に対する各種設定はInit()呼び出し後となる)
                sts = senseManager.Init();
                if (sts < pxcmStatus.PXCM_STATUS_NO_ERROR) throw new Exception("パイプラインの初期化に失敗しました");

                //ミラー表示にする
                senseManager.QueryCaptureManager().QueryDevice().SetMirrorMode(
                    PXCMCapture.Device.MirrorMode.MIRROR_MODE_HORIZONTAL);

                //デバイスを取得する
                device = senseManager.captureManager.device;

                //座標変換オブジェクトを作成
                projection = device.CreateProjection();

                // 手の検出の初期化
                InitializeHandTracking();
                Constants.RealSenseIsConnect = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary> 手の検出の初期化 </summary>
        private void InitializeHandTracking()
        {
            // 手の検出器を取得する
            handAnalyzer = senseManager.QueryHand();
            if (handAnalyzer == null)
            {
                throw new Exception("手の検出器の取得に失敗しました");
            }

            // 手のデータを作成する
            handData = handAnalyzer.CreateOutput();
            if (handData == null)
            {
                throw new Exception("手の検出器の作成に失敗しました");
            }

            // RealSense カメラであれば、プロパティを設定する
            var device = senseManager.QueryCaptureManager().QueryDevice();
            PXCMCapture.DeviceInfo dinfo;
            device.QueryDeviceInfo(out dinfo);
            if (dinfo.model == PXCMCapture.DeviceModel.DEVICE_MODEL_IVCAM)
            {
                device.SetDepthConfidenceThreshold(1);
                //device.SetMirrorMode( PXCMCapture.Device.MirrorMode.MIRROR_MODE_DISABLED );
                device.SetIVCAMFilterOption(6);
            }

            // 手の検出の設定
            config = handAnalyzer.CreateActiveConfiguration();
            //config.EnableSegmentationImage(true);
            config.EnableJointSpeed(PXCMHandData.JointType.JOINT_MIDDLE_TIP,PXCMHandData.JointSpeedType.JOINT_SPEED_AVERAGE, 100);
            //config.EnableGesture("v_sign");
            config.EnableGesture("thumb_up");
            config.EnableGesture("thumb_down");
            //config.EnableGesture("tap");
            //config.EnableGesture("fist");
            config.SubscribeGesture(OnFiredGesture);
            config.ApplyChanges();
            config.Update();
            gestureTimer.Start();
        }

        public System.Diagnostics.Stopwatch gestureTimer = new System.Diagnostics.Stopwatch();
        //long preOccuredGesture = 0;
        bool playEnsemble = false;
        private void OnFiredGesture(PXCMHandData.GestureData gestureData)
        {
            int side = Array.IndexOf(side2id, gestureData.handId);
            if (gestureData.name == "v_sign" && !playEnsemble)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    StartEnsemble();
                }));
                playEnsemble = true;
            }
            if (gestureData.name == "thumb_up" && gestureTimer.ElapsedMilliseconds > 1000)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    InstrumentsList.SelectedIndex = (InstrumentsList.SelectedIndex + 1) % InstrumentsList.Items.Count;
                }));
            }
            if (gestureData.name == "thumb_down" && gestureTimer.ElapsedMilliseconds > 1000)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    InstrumentsList.SelectedIndex--;
                }));
            }
            gestureTimer.Restart();
        }

        /// <summary> RealSesnseの更新 </summary>
        private void UpdateRealSense()
        {
            //フレームを取得する
            //AcquireFrame()の引数はすべての機能の更新が終るまで待つかどうかを指定
            //ColorやDepthによって更新間隔が異なるので設定によって値を変更
            var ret = senseManager.AcquireFrame(true);
            if (ret < pxcmStatus.PXCM_STATUS_NO_ERROR) return;

            //フレームデータを取得する
            PXCMCapture.Sample sample = senseManager.QuerySample();
            if (sample != null)
            {
                //カラー画像の表示
                UpdateColorImage(sample.color);
            }
            //手のデータを更新
            UpdateHandFrame();

            //演奏領域の表示
            //if (ensembleTimer.IsEnabled)
            if (ensembleTimer.IsEnabled)
                for (int k = 0; k < currentFreqs.Length; k++)
                {
                    SolidColorBrush myBrush = new SolidColorBrush(Constants.colors[k]);
                    myBrush.Opacity = 0.25;
                    AddRectangle(
                        ColorImage.Height / currentFreqs.Length * k,
                        ColorImage.Height / currentFreqs.Length,
                        ColorImage.Width,
                        Brushes.Black,
                        1.0d,
                        myBrush);
                }

            //フレームを解放する
            senseManager.ReleaseFrame();
        }

        /// <summary> カラーイメージが更新された時の処理 </summary>
        private void UpdateColorImage(PXCMImage colorFrame)
        {
            if (colorFrame == null) return;
            //データの取得
            PXCMImage.ImageData data;

            //アクセス権の取得
            pxcmStatus ret = colorFrame.AcquireAccess(
                PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, out data);
            if (ret < pxcmStatus.PXCM_STATUS_NO_ERROR) throw new Exception("カラー画像の取得に失敗");

            //ビットマップに変換する
            //画像の幅と高さ，フォーマットを取得
            var info = colorFrame.QueryInfo();

            //1ライン当たりのバイト数を取得し(pitches[0]) 高さをかける　(1pxel 3byte)
            var length = data.pitches[0] * info.height;

            //画素の色データの取得
            //ToByteArrayでは色データのバイト列を取得する．
            var buffer = data.ToByteArray(0, length);
            //バイト列をビットマップに変換
            ColorImage.Source = BitmapSource.Create(info.width, info.height, 96, 96, PixelFormats.Bgr32, null, buffer, data.pitches[0]);

            //データを解放する
            colorFrame.ReleaseAccess(data);
        }

        int[] side2id = new int[2];
        /// <summary> 手のデータを更新する </summary>
        private void UpdateHandFrame()
        {
            // 手のデータを更新する
            handData.Update();

            // データを初期化する
            PartsCanvas.Children.Clear();

            // 検出した手の数を取得する
            var numOfHands = handData.QueryNumberOfHands();
            for (int i = 0; i < numOfHands; i++)
            {
                // 手を取得する
                PXCMHandData.IHand hand;
                var sts = handData.QueryHandData(
                    PXCMHandData.AccessOrderType.ACCESS_ORDER_BY_ID, i, out hand);
                if (sts < pxcmStatus.PXCM_STATUS_NO_ERROR)
                {
                    continue;
                }
                //Console.WriteLine(hand.QueryUniqueId());
                int side = (int)hand.QueryBodySide();
                if(side > 0)side2id[side-1] = hand.QueryUniqueId();
                GetFingerData(hand, PXCMHandData.JointType.JOINT_MIDDLE_TIP);
            }
        }

        /// <summary> 指のデータを取得する </summary>
        private void GetFingerData(PXCMHandData.IHand hand, PXCMHandData.JointType jointType)
        { 
            PXCMHandData.JointData jointData;
            var sts = hand.QueryTrackedJoint(jointType, out jointData);
            if (sts < pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                return;
            }

            // Depth座標系をカラー座標系に変換する
            var depthPoint = new PXCMPoint3DF32[1];
            var colorPoint = new PXCMPointF32[1];
            depthPoint[0].x = jointData.positionImage.x;
            depthPoint[0].y = jointData.positionImage.y;
            depthPoint[0].z = jointData.positionWorld.z * 1000;
            projection.MapDepthToColor(depthPoint, colorPoint);

            var masp = hand.QueryMassCenterImage();
            var mdp = new PXCMPoint3DF32[1];
            var mcp = new PXCMPointF32[1];

            mdp[0].x = masp.x;
            mdp[0].y = masp.y;
            mdp[0].z = hand.QueryMassCenterWorld().z * 1000;

            projection.MapDepthToColor(mdp, mcp);
            //Console.WriteLine(mcp[0].x);
            AddEllipse(new Point(mcp[0].x, mcp[0].y), 10, Brushes.Red, 1);
            colorPoint = mcp;

            //AddEllipse(new Point(colorPoint[0].x, colorPoint[0].y), 5, Brushes.White, 1);

            if (ensembleTimer.IsEnabled) DetectTap(hand,mcp[0]);
            //if (ensembleTimer.IsEnabled) RecogMove(hand,mcp[0]);
            //RecogMove(hand,mcp[0]);
        }

        #region tap
        public System.Diagnostics.Stopwatch rsw = new System.Diagnostics.Stopwatch();
        public System.Diagnostics.Stopwatch lsw = new System.Diagnostics.Stopwatch();

        public PXCMPoint3DF32 RightCenter = new PXCMPoint3DF32();        //手のひら
        public PXCMPoint3DF32 preRightCenter = new PXCMPoint3DF32();
        public PXCMPoint3DF32 LeftCenter = new PXCMPoint3DF32();
        public PXCMPoint3DF32 preLeftCenter = new PXCMPoint3DF32();
        public PXCMPoint3DF32 RightMiddle = new PXCMPoint3DF32();       //中指の先
        public PXCMPoint3DF32 preRightMiddle = new PXCMPoint3DF32();
        public PXCMPoint3DF32 LeftMiddle = new PXCMPoint3DF32();
        public PXCMPoint3DF32 preLeftMiddle = new PXCMPoint3DF32();
        private void DetectTap(PXCMHandData.IHand hand , PXCMPointF32 mcp)
        {
            PXCMHandData.JointData midData;
            PXCMHandData.JointData cntData;

            //指のデータをとってくる(depth)
            //ユーザの右手のデータ
            if (hand.QueryBodySide() == PXCMHandData.BodySideType.BODY_SIDE_LEFT)
            {
                hand.QueryTrackedJoint(PXCMHandData.JointType.JOINT_CENTER, out cntData);
                RightCenter = cntData.positionWorld;
                hand.QueryTrackedJoint(PXCMHandData.JointType.JOINT_MIDDLE_TIP, out midData);
                RightMiddle = midData.positionWorld;
            }

            //ユーザの左手のデータ
            if (hand.QueryBodySide() == PXCMHandData.BodySideType.BODY_SIDE_RIGHT)
            {
                hand.QueryTrackedJoint(PXCMHandData.JointType.JOINT_CENTER, out cntData);
                LeftCenter = cntData.positionWorld;
                hand.QueryTrackedJoint(PXCMHandData.JointType.JOINT_MIDDLE_TIP, out midData);
                LeftMiddle = midData.positionWorld;
            }

            if (mcp.y < 0) mcp.y = 0;
            if (mcp.y > ColorImage.Height) mcp.y = (float)ColorImage.Height;

            //Console.WriteLine(rsw.ElapsedMilliseconds);
            //if文の条件を記述(前の指のデータと比較)
            // ユーザの右手でタップ
            if (-RightMiddle.z + preRightMiddle.z > 0.02                                  // 1F(約1/60秒)あたりの深度の変化が0.02m以上
                && Math.Pow(Math.Pow(RightMiddle.x - preRightMiddle.x, 2)                 // 指先の速度が1.8m/s以上
                                   + Math.Pow(RightMiddle.y - preRightMiddle.y, 2)
                                   + Math.Pow(RightMiddle.z * 1000 - preRightMiddle.z * 1000, 2), 0.5) > 0.03
                && Math.Pow(Math.Pow(RightCenter.x - preRightCenter.x, 2)                 // 手のひらの速度が0.6m/s以上
                                   + Math.Pow(RightCenter.y - preRightCenter.y, 2)
                                   + Math.Pow(RightCenter.z * 1000 - preRightCenter.z * 1000, 2), 0.5) > 0.01
                && rsw.ElapsedMilliseconds > 300
               )
            {
                //tap音を出力
                midi.OnNote(currentFreqs[(int)(((ColorImage.Height-mcp.y)/(ColorImage.Height+0.01))*currentFreqs.Length)]);
                rsw.Restart();
            }
            //var c = ColorImage.Height - y;
            //var e = c / ColorImage.Height;
            //var d = e * currentFreqs.Length;
            //Index.Text = y + "\n" + d + "\n" + (int)d;
            //Index.Text = mcp.y.ToString();
            //Chord.Text = e.ToString();
            // ユーザの左手でタップ
            if (-LeftMiddle.z + preLeftMiddle.z > 0.02                                  // 1F(約1/60秒)あたりの深度の変化が0.02m以上
                && Math.Pow(Math.Pow(LeftMiddle.x - preLeftMiddle.x, 2)                 // 指先の速度が1.8m/s以上
                                   + Math.Pow(LeftMiddle.y - preLeftMiddle.y, 2)
                                   + Math.Pow(LeftMiddle.z * 1000 - preLeftMiddle.z * 1000, 2), 0.5) > 0.03
                && Math.Pow(Math.Pow(LeftCenter.x - preLeftCenter.x, 2)                 // 手のひらの速度が0.6m/s以上
                                   + Math.Pow(LeftCenter.y - preLeftCenter.y, 2)
                                   + Math.Pow(LeftCenter.z * 1000 - preLeftCenter.z * 1000, 2), 0.5) > 0.01
                && lsw.ElapsedMilliseconds > 300
               )
            {
                //tap音を出力
                //midi.OnNote(currentFreqs[(int)(((ColorImage.Height - mcp.y) / (ColorImage.Height + 0.01)) * currentFreqs.Length)]);
                lsw.Restart();
            }

            //Console.WriteLine("RightCenter.x:" + RightCenter.x);
            //Console.WriteLine("preRightCenter.x:" + preRightCenter.x);

            //Console.WriteLine();
            //Console.WriteLine("RightMiddle.x:" + RightMiddle.x);
            //Console.WriteLine("preRightMiddle.x:" + preRightMiddle.x);


            //前の指のデータに今の指のデータを上書き
            //plc,preLeftMiddle,preRightCenter,preRightMiddle
            // 上手くいかなければディープコピーする．
            preRightCenter.x = RightCenter.x;
            preRightCenter.y = RightCenter.y;
            preRightCenter.z = RightCenter.z;
            preRightMiddle.x = RightMiddle.x;
            preRightMiddle.y = RightMiddle.y;
            preRightMiddle.z = RightMiddle.z;
            preLeftCenter.x = LeftCenter.x;
            preLeftCenter.y = LeftCenter.y;
            preLeftCenter.z = LeftCenter.z;
            preLeftMiddle.x = LeftMiddle.x;
            preLeftMiddle.y = LeftMiddle.y;
            preLeftMiddle.z = LeftMiddle.z;

        }
        #endregion

        PXCMHandData.JointData[] middleData = new PXCMHandData.JointData[2];
        PXCMHandData.JointData[] oldMiddleData = new PXCMHandData.JointData[2];

        private void RecogMove(PXCMHandData.IHand hand, PXCMPointF32 mcp)
        {
            int side = (int)hand.QueryBodySide() - 1;
            if (side < 0) return;

            hand.QueryTrackedJoint(PXCMHandData.JointType.JOINT_MIDDLE_TIP, out middleData[side]);
            if (oldMiddleData[side] == null)
            {
                oldMiddleData[side] = middleData[side];
                return;
            }
            //Console.WriteLine(middleData[side].speed.x);
            var distance = Math.Pow(
                Math.Pow(middleData[side].positionWorld.x - oldMiddleData[side].positionWorld.x, 2)
                + Math.Pow(middleData[side].positionWorld.y - oldMiddleData[side].positionWorld.y, 2)
                + Math.Pow((middleData[side].positionWorld.z - oldMiddleData[side].positionWorld.z) * 1000, 2),
                0.5 );

            oldMiddleData[side] = middleData[side];
            //Console.Write(middleData[side].positionImage.x + ":" + oldMiddleData[side].positionImage.x + ":");
            //Console.WriteLine(middleData[side].positionImage.x - oldMiddleData[side].positionImage.x);
            //oldMiddleData = middleData;
        }

        /// <summary> 円を表示する </summary>
        private void AddEllipse(Point point, int radius, Brush color, int thickness)
        {
            var ellipse = new Ellipse()
            {
                Width = radius,
                Height = radius,
            };
            if (thickness <= 0)
            {
                ellipse.Fill = color;
            }
            else
            {
                ellipse.Stroke = Brushes.Black;
                ellipse.StrokeThickness = thickness;
                ellipse.Fill = color;
            }
            Canvas.SetLeft(ellipse, point.X);
            Canvas.SetTop(ellipse, point.Y);
            PartsCanvas.Children.Add(ellipse);
        }

        /// <summary> 四角を表示する </summary>
        private void AddRectangle(double y, double height, double width, Brush stroke, double thickness, Brush fill)
        {
            Rectangle rect = new Rectangle
            {
                Width = width,
                Height = height,
                Stroke = stroke,
                StrokeThickness = thickness,
                Fill = fill
            };
            Canvas.SetTop(rect, y);
            PartsCanvas.Children.Add(rect);
        }
        #endregion

        #region Bluetooth

        private void OpenBluetoothWindow()
        {
            if (Constants.BluetoothWindowIsOpen) return;
            bluetoothWindow = new BluetoothWindow(this);
            bluetoothWindow.Show();
        }

        public void UpdateNTPTime()
        {
            // UDP生成
            System.Net.Sockets.UdpClient objSck;
            System.Net.IPEndPoint ipAny = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
            objSck = new System.Net.Sockets.UdpClient(ipAny);

            // UDP送信
            Byte[] sdat = new Byte[48];
            sdat[0] = 0xB;
            objSck.Send(sdat, sdat.GetLength(0), "ntp.nict.jp", 123);

            // UDP受信
            Byte[] rdat = objSck.Receive(ref ipAny);

            // 1900年1月1日からの経過時間(日時分秒)
            long lngAllS; // 1900年1月1日からの経過秒数
            long lngD;    // 日
            long lngH;    // 時
            long lngM;    // 分
            long lngS;    // 秒

            // 1900年1月1日からの経過秒数
            lngAllS = (long)(rdat[40] * (double)16777216 //2^24 Math.Pow(2, (8 * 3))
                    + rdat[41] * (double)65536    //2^16    
                    + rdat[42] * (double)256      //2^8 
                    + rdat[43]);

            /*
            lngAllS = (long)(rdat[40] * Math.Pow(2, (8 * 3)) //2^24
                    + rdat[41] * Math.Pow(2, (8 * 2))    //2^16    
                    + rdat[42] * Math.Pow(2, (8 * 1))      //2^8 
                    + rdat[43]);
                    */

            lngD = lngAllS / (24 * 60 * 60); // 日
            lngS = lngAllS % (24 * 60 * 60); // 残りの秒数
            lngH = lngS / (60 * 60);         // 時
            lngS = lngS % (60 * 60);         // 残りの秒数
            lngM = lngS / 60;                // 分
            lngS = lngS % 60;                // 秒

            long pico = (long)(rdat[44] * (double)16777216   //2^24
                        + rdat[45] * (double)65536    //2^16    
                        + rdat[46] * (double)256      //2^8 
                        + rdat[47]);

            long mill = (long)((pico * 1000) / (double)4294967296); //2~32

            // DateTime型への変換
            ntpBasicTime = ntpBasicTime.AddDays(lngD);
            ntpBasicTime = ntpBasicTime.AddHours(lngH);
            ntpBasicTime = ntpBasicTime.AddMinutes(lngM);
            ntpBasicTime = ntpBasicTime.AddSeconds(lngS);
            ntpBasicTime = ntpBasicTime.AddMilliseconds(mill);
            //グリニッジ標準時から日本時間への変更
            ntpBasicTime = ntpBasicTime.AddHours(9);

            Console.WriteLine("ntp time in UpdateNTPTime :" + ntpBasicTime.ToString());
            Console.WriteLine("local time in UpdateNTPTime :" + DateTime.Now.ToString());

            sw.Start();
        }
        #endregion

        private void InstrumentsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InstrumentsList.SelectedIndex > 0) midi.ProgramChange(Instruments.Numbers[InstrumentsList.SelectedIndex]);
        }

    }
}

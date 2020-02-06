using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAPICodePack.Dialogs;
using WpfBindingSample.Utils;

namespace WpfBindingSample
{
    class MainWindowViewModel : ViewModelBase
    {
        private string _Text = "同じプロパティのテキストを共有しているので，変更が共有されます．";
        public string Text
        {
            // Data Bindingをするためには，getterとsetterがpublicなフィールドを用意して，
            // setterで値が変化したらOnPropertyChanged()を呼ぶようにします．
            get => _Text;
            set
            {
                if (_Text == value) return;
                _Text = value;
                OnPropertyChanged();
                TextHalf.RaiseCanExecuteChanged();
            }
        }

        private int _Progress = 50;
        public int Progress
        {
            get => _Progress;
            set
            {
                if (_Progress == value) return;
                _Progress = value;
                OnPropertyChanged();
            }
        }

        public ViewModelCommand TextAdd { get; }

        public ViewModelCommand TextHalf { get; }

        public ViewModelCommand FolderOpenCommand { get; }

        public ViewModelCommand ProcessImageCommand { get; }

        public ViewModelCommand AverageImageCommand { get; }

        public ViewModelCommand VarianceImageCommand { get; }

        public ViewModelCommand NextImageCommand { get; }

        private Bitmap _Image;

        private Bitmap m_AverageImage;
        private Bitmap m_VarianceImage;
        private double[,] m_InputImageAverageArray;
        private double[] m_AverageImageAverage;
        private double[] m_VarianceImageAverage;

        private bool[] m_ExcludeImageFlagArray; // bool[]型の変数
        private int m_Num_UnExcludeImage;   // int型の変数

        private int n = 3;  //任意の繰り返し回数

        // 画像の切り替え表示用インデックス
        //

        private int _index = 0;
        public int Index
        {
            get => _index;
            set
            {
                if (_index == value) return;
                _index = value;
                OnPropertyChanged();
            }
        }

        //
        // 読み込んだ画像配列
        //
        private Bitmap[] m_InputImageArray;

        //
        // 画像のサイズ
        //  読み込んだ画像のサイズは全て同じものとする
        //
        private int m_Width, m_Height;

        //
        // 画像の画素数
        //
        private int m_NumPixel;

        public Bitmap Image
        {
            get => _Image;
            set
            {
                _Image = value;
                OnPropertyChanged();
                ProcessImageCommand.RaiseCanExecuteChanged();
                AverageImageCommand.RaiseCanExecuteChanged();
                VarianceImageCommand.RaiseCanExecuteChanged();
                NextImageCommand.RaiseCanExecuteChanged();
            }
        }

        public MainWindowViewModel()
        {
            // いつでも実行できるコマンド
            TextAdd = new ViewModelCommand(
                () => Text += ".");

            // 実行可能条件がついたコマンド
            // 実行可能かの変化時(ここでは，Textのsetter)に，RaiseCanExecuteChanged()を呼ぶ．
            TextHalf = new ViewModelCommand(
                () => Text = Text.Substring(0, Text.Length / 2),
                () => Text.Length != 0);

            FolderOpenCommand = new ViewModelCommand(() =>
            {
                var dialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    DefaultDirectory = App.Current.StartupUri.LocalPath,
                    EnsureReadOnly = true,
                    AllowNonFileSystemItems = false
                };



                //ディレクトリのパスを取得
                string path;
                DirectoryInfo dir;
                var result = dialog.ShowDialog(); //ダイアログを表示し，ユーザ操作の結果を返す
                if (result == CommonFileDialogResult.Ok)
                {
                    path = dialog.FileName; //選択されたフォルダのパスを取得
                    dir = new DirectoryInfo(path);
                    if (!dir.Exists)
                    {
                        return; //ディレクトリが存在しない場合は何もしない
                    }
                }
                else
                {
                    return; //キャンセルしたりダイアログが閉じられた場合は何もしない
                }

                //
                // フォルダパスから、フォルダ内のjpgファイルパスを全て取得する
                //
                var FileInfo = dir.GetFiles("*.jpg");
                string[] ImageFilePath = new string[FileInfo.Length];

                for (int i = 0; i < FileInfo.Length; i++)
                {
                    //Console.WriteLine(FileInfo[i] + " : " + FileInfo[i].FullName);
                    ImageFilePath[i] = FileInfo[i].FullName;
                }

                //
                // ビットマップ配列を生成し、全ての画像を読み込む
                //
                m_InputImageArray = new Bitmap[ImageFilePath.Length];
                for (int i = 0; i < ImageFilePath.Length; i++)
                {
                    m_InputImageArray[i] = new Bitmap(ImageFilePath[i]);
                }

                //
                // 画像に関するデータの変数を格納する
                //

                //
                // ピクチャーボックスに最初の画像を設定する
                // 表示画像の切り替え方法
                // 　Image = [Bitmapの変数];
                //
                Image = m_InputImageArray[0];

                m_Width = m_InputImageArray[Index].Width; //画像の横の長さ
                m_Height = m_InputImageArray[Index].Height; //画像の縦の長さ
                m_NumPixel = m_Width * m_Height;  //画像の総ピクセル数


                // 入力画像の数で初期化
                m_ExcludeImageFlagArray = new bool[m_InputImageArray.Length];

                // 入力画像の数を代入
                m_Num_UnExcludeImage = m_InputImageArray.Length;
            });

            ProcessImageCommand = new ViewModelCommand(() => Task.Run(() =>
            {
                if (m_InputImageArray == null) return;//画像が読み込まれていなければ処理しない

                CalcImageAverage();
                CalcImageVariance();
                CalcPixelAverage();
                ExcludeFrame();
                CalcImageAverage();
                OnPropertyChanged();
                m_AverageImage.Save("result.bmp");
            }),
                () => Image != null);

            AverageImageCommand = new ViewModelCommand(() => Task.Run(() =>
            {
                if (m_InputImageArray == null) return;//画像が読み込まれていなければ処理しない
                                                      //
                                                      // 画像処理実行コード
                                                      //
                CalcImageAverage();
                Image = m_AverageImage;
            }),
                () => Image != null);

            NextImageCommand = new ViewModelCommand(() =>
            {
                if (m_InputImageArray == null) return;
                Index++;
                if (m_InputImageArray.Length - 1 < Index) Index = 0;
                //
                // 表示画像の切り替え方法
                // 　pictureBox1.Image = [Bitmapの変数];
                //
                Image = m_InputImageArray[Index]; ;
            },
                () => Image != null);

            VarianceImageCommand = new ViewModelCommand(() => Task.Run(() =>
            {
                if (m_InputImageArray == null) return;//画像が読み込まれていなければ処理しない
                                                      //
                                                      // 画像処理実行コード
                                                      //
                CalcImageVariance();
                Image = m_VarianceImage;
            }),
                () => Image != null);
        }
        //平均画像を計算するメソッド
        private void CalcImageAverage()
        {
            if (m_InputImageArray == null) return; //画像が読み込まれていなければ処理しない

            m_AverageImage = new Bitmap(m_Width, m_Height); // m_AverageImageのインスタンス化

            double[,,] color_sum = new double[m_Width, m_Height, 3];   // 累積計算用のバッファ

            // 平均の計算
            for (int y = 0; y < m_Height; y++)
            {
                for (int x = 0; x < m_Width; x++)
                {
                    for (int i = 0; i < m_InputImageArray.Length; i++)
                    {
                        // m_ExcludeImageArrayの該当要素がtrueなら飛ばす
                        if (m_ExcludeImageFlagArray[i] == true)
                        {
                            continue;
                        }

                        var pixelColor = m_InputImageArray[i].GetPixel(x, y);   // 色取得

                        color_sum[x, y, 0] += pixelColor.R;
                        color_sum[x, y, 1] += pixelColor.G;
                        color_sum[x, y, 2] += pixelColor.B;
                    }

                    // 累積について画像の数で除算
                    var r = (int)(color_sum[x, y, 0] / m_Num_UnExcludeImage);
                    var g = (int)(color_sum[x, y, 1] / m_Num_UnExcludeImage);
                    var b = (int)(color_sum[x, y, 2] / m_Num_UnExcludeImage);

                    // 各画素に値を格納
                    m_AverageImage.SetPixel(x, y, Color.FromArgb(r, g, b));
                    Progress = (int)((y * m_Width + x) / (float)(m_Width * m_Height) * 100);
                }
            }
        }
        //分散画像を計算するメソッド
        private void CalcImageVariance()
        {
            if (m_InputImageArray == null) return;//画像が読み込まれていなければ処理しない

            // インスタンス化
            m_VarianceImage = new Bitmap(m_Width, m_Height);

            // バッファ生成
            double[,,] colorVariance = new double[m_Width, m_Height, 3];

            var maxVariance = 0.0d;     //分散の最大値

            //すべてのピクセルについて
            for (int y = 0; y < m_Height; y++)
            {
                for (int x = 0; x < m_Width; x++)
                {
                
                    double redSum, greenSum, blueSum;                //平均との差の二乗の合計値を保持する一時変数
                    redSum = greenSum = blueSum = 0;                 //↑を0で初期化
                    var avePixel = m_AverageImage.GetPixel(x, y);      //平均の画素

                    //RGBをそれぞれ枚数分合計
                    for (int i = 0; i < m_InputImageArray.Length; i++)
                    {
                        // m_ExcludeImageArrayの該当要素がtrueなら飛ばす
                        if (m_ExcludeImageFlagArray[i] == true)
                        {
                            continue;
                        }

                        var pixelColor = m_InputImageArray[i].GetPixel(x, y);
                        redSum += (avePixel.R - pixelColor.R) * (avePixel.R - pixelColor.R);
                        greenSum += (avePixel.G - pixelColor.G) * (avePixel.G - pixelColor.G);
                        blueSum += (avePixel.B - pixelColor.B) * (avePixel.B - pixelColor.B);
                    }

                    //平均との差の二乗を枚数で割る(平均) = 分散

                    colorVariance[x, y, 0] = redSum / m_Num_UnExcludeImage;
                    colorVariance[x, y, 1] = greenSum / m_Num_UnExcludeImage;
                    colorVariance[x, y, 2] = blueSum / m_Num_UnExcludeImage;

                    // 分散の最大値を更新
                    maxVariance = Math.Max(maxVariance,
                                    Math.Max(colorVariance[x, y, 0],
                                        Math.Max(colorVariance[x, y, 1],
                                            colorVariance[x, y, 2])));
                    Progress = (int)((y * m_Width + x) / (float)(m_Width * m_Height) * 100);
                }

            }

            for (int y = 0; y < m_Height; y++)
            {
                for (int x = 0; x < m_Width; x++)
                {
                    //値を0~255に正規化して画像を生成
                    var red = (int)(colorVariance[x, y, 0] * 255 / maxVariance);
                    var green = (int)(colorVariance[x, y, 1] * 255 / maxVariance);
                    var blue = (int)(colorVariance[x, y, 2] * 255 / maxVariance);
                    m_VarianceImage.SetPixel(x, y, Color.FromArgb(red, green, blue));
                    Progress = (int)((y * m_Width + x) / (float)(m_Width * m_Height) * 100);
                }
            }
        }

        // 画素平均を計算するメソッド
        private void CalcPixelAverage()
        {
            m_InputImageAverageArray = new double[m_InputImageArray.Length, 3]; //m_ImputImageArrayのそれぞれの画像の色の平均値を保存
            m_AverageImageAverage = new double[3];  //m_AverageImageの画像の色の平均値を保存
            m_VarianceImageAverage = new double[3]; //m_VarianceImageの画像の色の平均値を保存

            var colorSumArr = new double[m_InputImageArray.Length, 3]; //m_ImputImageArrayの色の値の合計値を保存するためのバッファ
            var colorSumAve = new double[3];    //m_AverageImageの色の値の合計値を保存するためのバッファ
            var colorSumVar = new double[3];    //m_VarianceImageの色の値の合計値を保存するためのバッファ

            /** m_ImputImageArrayのそれぞれの画像の色の平均値を求める **/
            for (int i = 0; i < m_InputImageArray.Length; i++)
            {
                for (int y = 0; y < m_Height; y++)
                {
                    for (int x = 0; x < m_Width; x++)
                    {
                    
                        var pixelColor = m_InputImageArray[i].GetPixel(x, y);
                        colorSumArr[i, 0] += pixelColor.R;
                        colorSumArr[i, 1] += pixelColor.G;
                        colorSumArr[i, 2] += pixelColor.B;
                        Progress = (int)((y * m_Width + x) / (float)(m_Width * m_Height) * 100);
                    }
                }
                m_InputImageAverageArray[i, 0] = colorSumArr[i, 0] / m_NumPixel;
                m_InputImageAverageArray[i, 1] = colorSumArr[i, 1] / m_NumPixel;
                m_InputImageAverageArray[i, 2] = colorSumArr[i, 2] / m_NumPixel;


                if (i == 0 || i == 8 || i == 14)
                {
                    Console.WriteLine("[{0}, 0] = " + m_InputImageAverageArray[i, 0], i);
                    Console.WriteLine("[{0}, 1] = " + m_InputImageAverageArray[i, 1], i);
                    Console.WriteLine("[{0}, 2] = " + m_InputImageAverageArray[i, 2], i);
                }
            }

            /** m_AverageImageとm_VarianceImageのそれぞれの画像の色の平均値を求める **/
            for (int y = 0; y < m_Height; y++)
            {
                for (int x = 0; x < m_Width; x++)
                {
                
                    var pixelColorAve = m_AverageImage.GetPixel(x, y);
                    var pixelColorVar = m_VarianceImage.GetPixel(x, y);

                    colorSumAve[0] += pixelColorAve.R;
                    colorSumAve[1] += pixelColorAve.G;
                    colorSumAve[2] += pixelColorAve.B;

                    colorSumVar[0] += pixelColorVar.R;
                    colorSumVar[1] += pixelColorVar.G;
                    colorSumVar[2] += pixelColorVar.B;
                    Progress = (int)((y * m_Width + x) / (float)(m_Width * m_Height) * 100);
                }
            }
            m_AverageImageAverage[0] = colorSumAve[0] / m_NumPixel;
            m_AverageImageAverage[1] = colorSumAve[1] / m_NumPixel;
            m_AverageImageAverage[2] = colorSumAve[2] / m_NumPixel;

            m_VarianceImageAverage[0] = colorSumVar[0] / m_NumPixel;
            m_VarianceImageAverage[1] = colorSumVar[1] / m_NumPixel;
            m_VarianceImageAverage[2] = colorSumVar[2] / m_NumPixel;

            Console.WriteLine("[0] = " + m_AverageImageAverage[0]);
            Console.WriteLine("[1] = " + m_AverageImageAverage[1]);
            Console.WriteLine("[2] = " + m_AverageImageAverage[2]);

            Console.WriteLine("[0] = " + m_VarianceImageAverage[0]);
            Console.WriteLine("[1] = " + m_VarianceImageAverage[1]);
            Console.WriteLine("[2] = " + m_VarianceImageAverage[2]);
        }

        // フレームを弾くメソッド
        private void ExcludeFrame()
        {
            //
            // 標準偏差の計算用
            //
            double[] standardDeviation = new double[3];
            double[] substructAverage = new double[3];
            // 標準偏差を計算
            standardDeviation[0] = Math.Sqrt(m_VarianceImageAverage[0]);
            standardDeviation[1] = Math.Sqrt(m_VarianceImageAverage[1]);
            standardDeviation[2] = Math.Sqrt(m_VarianceImageAverage[2]);
            //
            // 除外判定
            //

            m_Num_UnExcludeImage = 0;   // 繰り返し前に0を代入する

            for (int i = 0; i < m_InputImageArray.Length; i++)
            {
                //
                // 平均画像の平均画素と現在処理している画像の平均画素の差の絶対値を求める
                // Math.Absを忘れると2が"弾かない"になる
                //
                substructAverage[0] = Math.Abs(m_AverageImageAverage[0] - m_InputImageAverageArray[i, 0]);
                substructAverage[1] = Math.Abs(m_AverageImageAverage[1] - m_InputImageAverageArray[i, 1]);
                substructAverage[2] = Math.Abs(m_AverageImageAverage[2] - m_InputImageAverageArray[i, 2]);
                //
                // 条件判定
                //
                if (substructAverage[0] < standardDeviation[0] && substructAverage[1] < standardDeviation[1] && substructAverage[2] < standardDeviation[2])
                {
                    Console.WriteLine(i + "弾かない");

                    // 1増加する処理
                    m_Num_UnExcludeImage += 1;
                }
                else
                {
                    Console.WriteLine(i + "弾く");

                    // 該当要素をtrueに
                    m_ExcludeImageFlagArray[i] = true;
                }
                Progress = (int)((float)i / m_InputImageArray.Length * 100);
                OnPropertyChanged();
            }
        }
    }
}

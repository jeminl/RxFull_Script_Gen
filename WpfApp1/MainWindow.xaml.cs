using System;
using System.Collections.Generic;
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

using System.Data.OleDb;
using System.IO;
using System.Windows.Forms;
using System.Data.Common;
using System.Data;
using System.Globalization;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        cDictionalyfactory DicFactor = new cDictionalyfactory();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                if (ofd.ShowDialog()== System.Windows.Forms.DialogResult.OK )
                {
                    string FilePath = ofd.FileName;

                    DataTable _modifyDT = new DataTable();
                                        
                    _modifyDT = ConvertTCFfromCsvFile(GetDataTablefromCsvfile(FilePath, true));
                    ExportCVSfromDataTable(_modifyDT);

                    this.Close();
                }
            }
        }

        private DataTable GetDataTablefromCsvfile(string path, bool is1stRowHeader)
        {
            string header = is1stRowHeader ? "Yes" : "No";
            string PathOnly = System.IO.Path.GetDirectoryName(path);
            string filename = System.IO.Path.GetFileName(path);

            string SQL = @"SELECT * FROM [" + filename + "]";   

            using (OleDbConnection connection = new OleDbConnection(
                @"Provider=Microsoft.Jet.OLEDB.4.0; Data Source=" + PathOnly +
                ";Extended Properties=\"Text;HDR=" + header + "\""))
            using (OleDbCommand command = new OleDbCommand(SQL, connection))
            using (OleDbDataAdapter adapter = new OleDbDataAdapter(command))
            {
                DataTable dataTable = new DataTable("RawInput");
                dataTable.Locale = CultureInfo.CurrentCulture;
                adapter.Fill(dataTable);
                
                adapter.Dispose();

                return dataTable;
            }
        }

        private DataTable ConvertTCFfromCsvFile(DataTable dt)
        {
            //Data Table defined..
            DataTable _dt = new DataTable();
            foreach (string name in Enum.GetNames(typeof(TcfHeader)))
            {
                DataColumn nameColumn = new DataColumn(name, typeof(string));
                _dt.Columns.Add(nameColumn);
            }

            //Modify TCF
            foreach (DataRow _csvRow in dt.Rows)
            {
                //Get condition from input file each row
                CAcombination _CAcombination = new CAcombination();
                _CAcombination.Type = _csvRow["Type"].ToString();
                _CAcombination.MuxCase = _csvRow["MuxCase"].ToString();
                _CAcombination.Swplex  = _csvRow["SWPLEX"].ToString();

                foreach (var columnName in _csvRow.Table.Columns)
                {
                    if (columnName.ToString().Contains("LNA"))
                    {
                        if (!String.IsNullOrEmpty(_csvRow[columnName.ToString()].ToString()))
                        {
                            string dicBand = _csvRow[columnName.ToString()].ToString();
                            string dicPort = _csvRow[columnName.ToString().Split('_')[1] + "_RxPort"].ToString();

                            _CAcombination.BandPort.Add(dicBand, dicPort);
                        }
                    }
                }                

                Dictionary<string, int[]> AllPortAssign = new Dictionary<string, int[]>();
                Dictionary<string, string> AllModeAssign = new Dictionary<string, string>();

                foreach (string MeasureBand in _CAcombination.BandPort.Keys)
                {
                    string[] portEnable = _CAcombination.BandPort[MeasureBand].Split(',');
                    int[] intportEnable = new int[portEnable.Length];
                    intportEnable = Array.ConvertAll<string, int>(portEnable, int.Parse);
                    AllPortAssign.Add(MeasureBand, intportEnable);
                    AllModeAssign.Add(MeasureBand, "G0");
                }

                string[] MuxCase = null;

                switch(_CAcombination.MuxCase+ _CAcombination.Swplex)
                {
                    case "ALLEU": MuxCase = new string[] {"B7_EU","B41_EU","ISO_x"};break;
                    case "ALLUS": MuxCase = new string[] { "B7_US", "B41_US", "ISO_x" }; break;
                    case "B7ALL": MuxCase = new string[] { "B7_ONLY", "B7_EU", "B7_US" }; break;
                    case "B41ALL": MuxCase = new string[] { "B41_ONLY", "B41_EU", "B41_US" }; break;
                    case "ISO": MuxCase = new string[] { "ISO_x" }; break;
                    default:
                        MuxCase = new string[] { _CAcombination.MuxCase + "_"+ _CAcombination.Swplex };
                        break;
                }

                int MaxMode = 7; //Mode loop

                foreach (string strmux in MuxCase)
                {
                    for (int modeIndex1 = 0; modeIndex1 < MaxMode; modeIndex1++)
                    {
                        if (AllModeAssign.Count > 3)
                            AllModeAssign[AllModeAssign.Keys.ToList()[3].ToString()] = "G" + modeIndex1;
                        else modeIndex1 = 999;

                        for (int modeIndex2 = 0; modeIndex2 < MaxMode; modeIndex2++)
                        {
                            if (AllModeAssign.Count > 2)
                                AllModeAssign[AllModeAssign.Keys.ToList()[2].ToString()] = "G" + modeIndex2;
                            else modeIndex2 = 999;

                            for (int modeIndex3 = 0; modeIndex3 < MaxMode; modeIndex3++)
                            {
                                if (AllModeAssign.Count > 1)
                                    AllModeAssign[AllModeAssign.Keys.ToList()[1].ToString()] = "G" + modeIndex3;
                                else modeIndex3 = 999;

                                for (int modeIndex4 = 0; modeIndex4 < MaxMode; modeIndex4++)
                                {
                                    AllModeAssign[AllModeAssign.Keys.ToList()[0].ToString()] = "G" + modeIndex4;
                                    generateDataRow(_dt, _CAcombination, AllPortAssign, AllModeAssign, strmux);
                                }
                            }
                        }
                    }
                }
                

            }

            return _dt;
        }
        
        private DataTable generateDataRow (DataTable _cdt, CAcombination _cCAcombination ,  Dictionary<string, int[]> _AllPortAssign , Dictionary<string, string> _AllModeAssign,string muxcase)
        {


            switch (_AllPortAssign.Count)
            {
                case 1:
                    foreach (var _currentBand in _AllPortAssign.Keys)
                    {
                        foreach (int _currentPort in _AllPortAssign[_currentBand])
                        {
                            DataRow _Row = _cdt.NewRow();
                            foreach (string name in Enum.GetNames(typeof(TcfHeader))) _Row[name] = "";

                            _Row[EnumToInt(TcfHeader.BAND)] = _currentBand;
                            _Row[EnumToInt(TcfHeader.Freq)] = DicFactor.dicFreq[_currentBand];
                            _Row[EnumToInt(TcfHeader.Switch_RX)] = "OUT" + _currentPort;
                            _Row[EnumToInt(TcfHeader.SwPlex)] = _cCAcombination.Swplex;

                            _Row[EnumToInt(TcfHeader.REGCUSTOM)] = _currentBand + "OUT" + _currentPort;
                            _Row[EnumToInt(TcfHeader.Power_Mode)] = _AllModeAssign[_currentBand];
                            _Row[EnumToInt(TcfHeader.Note)] = muxcase + "_"  + _currentBand + "OUT" + _currentPort + _AllModeAssign[_currentBand];
                            
                            
                            if (_currentBand.Contains("B11") || _currentBand.Contains("B21"))
                                _Row[EnumToInt(TcfHeader.Switch_ANT)] = "ANT2";
                            else
                                _Row[EnumToInt(TcfHeader.Switch_ANT)] = "ANT1";

                            if (_AllModeAssign[_currentBand] == "G6")
                            {
                                _Row[EnumToInt(TcfHeader.Pin)] = "0";
                                _Row[EnumToInt(TcfHeader.ExpectedGain)] = "0";
                            }
                            else if(_AllModeAssign[_currentBand] == "G0"|| _AllModeAssign[_currentBand] == "G1"|| _AllModeAssign[_currentBand] == "G2")
                            {
                                _Row[EnumToInt(TcfHeader.Pin)] = "-25";
                                _Row[EnumToInt(TcfHeader.ExpectedGain)] = "20";
                            }
                            else if (_AllModeAssign[_currentBand] == "G3" || _AllModeAssign[_currentBand] == "G4" || _AllModeAssign[_currentBand] == "G5")
                            {
                                _Row[EnumToInt(TcfHeader.Pin)] = "-25";
                                _Row[EnumToInt(TcfHeader.ExpectedGain)] = "10";
                            }


                            _Row = mipigenerate(_Row);

                            _cdt.Rows.Add(_Row);
                        }
                    }
                    break;

                case 2:
                    foreach (string MeasureBand in _cCAcombination.BandPort.Keys)
                    {
                        foreach (int MeasurePort in _AllPortAssign[MeasureBand])
                        {
                            foreach (var _currentBand in _AllPortAssign.Keys)
                            {
                                if (MeasureBand == _currentBand) continue;

                                foreach (int currentPort in _AllPortAssign[_currentBand])
                                {
                                    Dictionary<string, int> currentPortAssign = new Dictionary<string, int>();
                                    currentPortAssign.Add(MeasureBand, MeasurePort);

                                    if (!currentPortAssign.ContainsValue(currentPort))
                                    {
                                        currentPortAssign.Add(_currentBand, currentPort);

                                        DataRow _Row = _cdt.NewRow();
                                        foreach (string name in Enum.GetNames(typeof(TcfHeader))) _Row[name] = "";

                                        _Row[EnumToInt(TcfHeader.BAND)] = MeasureBand;
                                        _Row[EnumToInt(TcfHeader.Freq)] = DicFactor.dicFreq[MeasureBand];
                                        _Row[EnumToInt(TcfHeader.Switch_RX)] = "OUT" + MeasurePort;
                                        _Row[EnumToInt(TcfHeader.SwPlex)] = _cCAcombination.Swplex;

                                        string _strCustom = "";
                                        string _strNote = "";
                                        //foreach (string _temp in currentPortAssign.Keys) _strCustom += _temp + "OUT" + currentPortAssign[_temp] + ".";
                                        foreach (var item in currentPortAssign.OrderBy(i => i.Key))
                                        {
                                            _strCustom += item.Key + "OUT" + item.Value + ".";
                                            _strNote += item.Key + "OUT" + item.Value + _AllModeAssign[item.Key] + "-";
                                        }

                                        _Row[EnumToInt(TcfHeader.REGCUSTOM)] = _strCustom.Substring(0, _strCustom.Length - 1);
                                        _Row[EnumToInt(TcfHeader.Power_Mode)] = _AllModeAssign[MeasureBand];
                                        _Row[EnumToInt(TcfHeader.Note)] = muxcase + "_" + _strNote.Substring(0, _strNote.Length - 1);

                                        if (MeasureBand.Contains("B11") || MeasureBand.Contains("B21"))
                                            _Row[EnumToInt(TcfHeader.Switch_ANT)] = "ANT2";
                                        else
                                            _Row[EnumToInt(TcfHeader.Switch_ANT)] = "ANT1";

                                        if (_AllModeAssign[MeasureBand] == "G6")
                                        {
                                            _Row[EnumToInt(TcfHeader.Pin)] = "0";
                                            _Row[EnumToInt(TcfHeader.ExpectedGain)] = "0";
                                        }
                                        else if (_AllModeAssign[MeasureBand] == "G0" || _AllModeAssign[MeasureBand] == "G1" || _AllModeAssign[MeasureBand] == "G2")
                                        {
                                            _Row[EnumToInt(TcfHeader.Pin)] = "-25";
                                            _Row[EnumToInt(TcfHeader.ExpectedGain)] = "20";
                                        }
                                        else if (_AllModeAssign[MeasureBand] == "G3" || _AllModeAssign[MeasureBand] == "G4" || _AllModeAssign[MeasureBand] == "G5")
                                        {
                                            _Row[EnumToInt(TcfHeader.Pin)] = "-25";
                                            _Row[EnumToInt(TcfHeader.ExpectedGain)] = "10";
                                        }

                                        _Row = mipigenerate(_Row);
                                        _cdt.Rows.Add(_Row);
                                    }
                                }
                            }
                        }
                    }
                    break;
                case 3:
                    Dictionary<string, bool> Allcustom = new Dictionary<string, bool>();
                    foreach (string MeasureBand in _cCAcombination.BandPort.Keys)
                    {
                        foreach (int MeasurePort in _AllPortAssign[MeasureBand])
                        {
                            foreach (string MeasureBand2 in _cCAcombination.BandPort.Keys)
                            {
                                if (MeasureBand == MeasureBand2) continue;

                                foreach (int MeasurePort2 in _AllPortAssign[MeasureBand2])
                                {
                                    if (MeasurePort == MeasurePort2) continue;

                                    foreach (var _currentBand in _AllPortAssign.Keys)
                                    {
                                        if (MeasureBand == _currentBand || MeasureBand2 == _currentBand) continue;

                                        foreach (int currentPort in _AllPortAssign[_currentBand])
                                        {
                                            Dictionary<string, int> currentPortAssign = new Dictionary<string, int>();
                                            currentPortAssign.Add(MeasureBand, MeasurePort);
                                            currentPortAssign.Add(MeasureBand2, MeasurePort2);

                                            if (!currentPortAssign.ContainsValue(currentPort))
                                            {
                                                currentPortAssign.Add(_currentBand, currentPort);

                                                DataRow _Row = _cdt.NewRow();
                                                foreach (string name in Enum.GetNames(typeof(TcfHeader))) _Row[name] = "";

                                                _Row[EnumToInt(TcfHeader.BAND)] = MeasureBand;
                                                _Row[EnumToInt(TcfHeader.Freq)] = DicFactor.dicFreq[MeasureBand];
                                                _Row[EnumToInt(TcfHeader.Switch_RX)] = "OUT" + MeasurePort;
                                                _Row[EnumToInt(TcfHeader.SwPlex)] = _cCAcombination.Swplex;

                                                string _strCustom = "";
                                                string _strNote = "";
                                                //foreach (string _temp in currentPortAssign.Keys) _strCustom += _temp + "OUT" + currentPortAssign[_temp] + ".";
                                                foreach (var item in currentPortAssign.OrderBy(i => i.Key))
                                                {
                                                    _strCustom += item.Key + "OUT" + item.Value + ".";
                                                    _strNote += item.Key + "OUT" + item.Value + _AllModeAssign[item.Key] + "-";
                                                }
                                                _Row[EnumToInt(TcfHeader.REGCUSTOM)] = _strCustom.Substring(0, _strCustom.Length - 1);
                                                _Row[EnumToInt(TcfHeader.Power_Mode)] = _AllModeAssign[MeasureBand];
                                                _Row[EnumToInt(TcfHeader.Note)] = muxcase + "_" + _strNote.Substring(0, _strNote.Length - 1);

                                                if (MeasureBand.Contains("B11") || MeasureBand.Contains("B21"))
                                                    _Row[EnumToInt(TcfHeader.Switch_ANT)] = "ANT2";
                                                else
                                                    _Row[EnumToInt(TcfHeader.Switch_ANT)] = "ANT1";

                                                if (_AllModeAssign[MeasureBand] == "G6")
                                                {
                                                    _Row[EnumToInt(TcfHeader.Pin)] = "0";
                                                    _Row[EnumToInt(TcfHeader.ExpectedGain)] = "0";
                                                }
                                                else if (_AllModeAssign[MeasureBand] == "G0" || _AllModeAssign[MeasureBand] == "G1" || _AllModeAssign[MeasureBand] == "G2")
                                                {
                                                    _Row[EnumToInt(TcfHeader.Pin)] = "-25";
                                                    _Row[EnumToInt(TcfHeader.ExpectedGain)] = "20";
                                                }
                                                else if (_AllModeAssign[MeasureBand] == "G3" || _AllModeAssign[MeasureBand] == "G4" || _AllModeAssign[MeasureBand] == "G5")
                                                {
                                                    _Row[EnumToInt(TcfHeader.Pin)] = "-25";
                                                    _Row[EnumToInt(TcfHeader.ExpectedGain)] = "10";
                                                }

                                                if (!Allcustom.ContainsKey(MeasureBand + "_" + _strCustom.Substring(0, _strCustom.Length - 1)))
                                                {
                                                    Allcustom.Add(MeasureBand + "_" + _strCustom.Substring(0, _strCustom.Length - 1), true);
                                                    _Row = mipigenerate(_Row);
                                                    _cdt.Rows.Add(_Row);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;
                case 4:
                    Dictionary<string, bool> Allcustom2 = new Dictionary<string, bool>();
                    foreach (string MeasureBand in _cCAcombination.BandPort.Keys)
                    {
                        foreach (int MeasurePort in _AllPortAssign[MeasureBand])
                        {
                            foreach (string MeasureBand2 in _cCAcombination.BandPort.Keys)
                            {
                                if (MeasureBand == MeasureBand2) continue;

                                foreach (int MeasurePort2 in _AllPortAssign[MeasureBand2])
                                {
                                    if (MeasurePort == MeasurePort2) continue;

                                    foreach (string MeasureBand3 in _cCAcombination.BandPort.Keys)
                                    {
                                        if (MeasureBand == MeasureBand3 || MeasureBand2 == MeasureBand3) continue;

                                        foreach (int MeasurePort3 in _AllPortAssign[MeasureBand3])
                                        {
                                            if (MeasurePort == MeasurePort3 || MeasurePort2 == MeasurePort3) continue;
                                                                                       
                                            foreach (var _currentBand in _AllPortAssign.Keys)
                                            {
                                                if (MeasureBand == _currentBand || MeasureBand2 == _currentBand || MeasureBand3 == _currentBand) continue;

                                                foreach (int currentPort in _AllPortAssign[_currentBand])
                                                {
                                                    Dictionary<string, int> currentPortAssign = new Dictionary<string, int>();
                                                    currentPortAssign.Add(MeasureBand, MeasurePort);
                                                    currentPortAssign.Add(MeasureBand2, MeasurePort2);
                                                    currentPortAssign.Add(MeasureBand3, MeasurePort3);

                                                    if (!currentPortAssign.ContainsValue(currentPort))
                                                    {
                                                        currentPortAssign.Add(_currentBand, currentPort);

                                                        DataRow _Row = _cdt.NewRow();
                                                        foreach (string name in Enum.GetNames(typeof(TcfHeader))) _Row[name] = "";

                                                        _Row[EnumToInt(TcfHeader.BAND)] = MeasureBand;
                                                        _Row[EnumToInt(TcfHeader.Freq)] = DicFactor.dicFreq[MeasureBand];
                                                        _Row[EnumToInt(TcfHeader.Switch_RX)] = "OUT" + MeasurePort;
                                                        _Row[EnumToInt(TcfHeader.SwPlex)] = _cCAcombination.Swplex;

                                                        string _strCustom = "";
                                                        string _strNote = "";
                                                        //foreach (string _temp in currentPortAssign.Keys) _strCustom += _temp + "OUT" + currentPortAssign[_temp] + ".";
                                                        foreach (var item in currentPortAssign.OrderBy(i => i.Key))
                                                        {
                                                            _strCustom += item.Key + "OUT" + item.Value + ".";
                                                            _strNote += item.Key + "OUT" + item.Value + _AllModeAssign[item.Key] + "-";
                                                        }
                                                        _Row[EnumToInt(TcfHeader.REGCUSTOM)] = _strCustom.Substring(0, _strCustom.Length - 1);
                                                        _Row[EnumToInt(TcfHeader.Power_Mode)] = _AllModeAssign[MeasureBand];
                                                        _Row[EnumToInt(TcfHeader.Note)] = muxcase + "_" + _strNote.Substring(0, _strNote.Length - 1);

                                                        if (MeasureBand.Contains("B11") || MeasureBand.Contains("B21"))
                                                            _Row[EnumToInt(TcfHeader.Switch_ANT)] = "ANT2";
                                                        else
                                                            _Row[EnumToInt(TcfHeader.Switch_ANT)] = "ANT1";

                                                        if (_AllModeAssign[MeasureBand] == "G6")
                                                        {
                                                            _Row[EnumToInt(TcfHeader.Pin)] = "0";
                                                            _Row[EnumToInt(TcfHeader.ExpectedGain)] = "0";
                                                        }
                                                        else if (_AllModeAssign[MeasureBand] == "G0" || _AllModeAssign[MeasureBand] == "G1" || _AllModeAssign[MeasureBand] == "G2")
                                                        {
                                                            _Row[EnumToInt(TcfHeader.Pin)] = "-25";
                                                            _Row[EnumToInt(TcfHeader.ExpectedGain)] = "20";
                                                        }
                                                        else if (_AllModeAssign[MeasureBand] == "G3" || _AllModeAssign[MeasureBand] == "G4" || _AllModeAssign[MeasureBand] == "G5")
                                                        {
                                                            _Row[EnumToInt(TcfHeader.Pin)] = "-25";
                                                            _Row[EnumToInt(TcfHeader.ExpectedGain)] = "10";
                                                        }

                                                        if (!Allcustom2.ContainsKey(MeasureBand + "_" + _strCustom.Substring(0, _strCustom.Length - 1)))
                                                        {
                                                            Allcustom2.Add(MeasureBand + "_" + _strCustom.Substring(0, _strCustom.Length - 1), true);
                                                            _Row = mipigenerate(_Row);
                                                            _cdt.Rows.Add(_Row);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                                                           
                    //foreach (string MeasureBand in _cCAcombination.BandPort.Keys)
                    //{
                    //    DataRow _Row = _cdt.NewRow();
                    //    foreach (string name in Enum.GetNames(typeof(TcfHeader))) _Row[name] = "";

                    //    _Row[EnumToInt(TcfHeader.BAND)] = MeasureBand;
                    //    _Row[EnumToInt(TcfHeader.Freq)] = DicFactor.dicFreq[MeasureBand];
                    //    _Row[EnumToInt(TcfHeader.SwPlex)] = _cCAcombination.Swplex;
                    //    switch (MeasureBand)
                    //    {
                    //        case "B1":
                    //            _Row[EnumToInt(TcfHeader.Switch_RX)] = "OUT3"; break;
                    //        case "B3":
                    //            _Row[EnumToInt(TcfHeader.Switch_RX)] = "OUT2"; break;
                    //        case "B7":
                    //            _Row[EnumToInt(TcfHeader.Switch_RX)] = "OUT4"; break;
                    //        case "B40":
                    //            _Row[EnumToInt(TcfHeader.Switch_RX)] = "OUT1"; break;
                    //    }
                    //    _Row[EnumToInt(TcfHeader.REGCUSTOM)] = "B1OUT3.B3OUT2.B7OUT4.B40OUT1";
                    //    _Row[EnumToInt(TcfHeader.Power_Mode)] = _AllModeAssign[MeasureBand];
                    //    _Row[EnumToInt(TcfHeader.Note)] = muxcase + "_"  + "B1OUT3" + _AllModeAssign["B1"] + "-" + "B3OUT2" + _AllModeAssign["B3"] + "-" + "B7OUT4" + _AllModeAssign["B7"] + "-" + "B40OUT1" + _AllModeAssign["B40"];
                    //    _Row = mipigenerate(_Row);
                    //    _cdt.Rows.Add(_Row);
                    //}

                    break;

                default: break;
            }

            return _cdt;
        }

        private DataRow mipigenerate(DataRow _dr)
        {

            string[] NoteArry = _dr[EnumToInt(TcfHeader.Note)].ToString().Split('_');

            //string MuxCase = NoteArry[0];
            string[] LNAArry = NoteArry[2].Split('-');
                                 
            //Tx04
            int intTx04 = 0;

            if (_dr[EnumToInt(TcfHeader.REGCUSTOM)].ToString().Contains("B21")
               || _dr[EnumToInt(TcfHeader.REGCUSTOM)].ToString().Contains("B11"))
            {
                intTx04 = DicFactor.Tx04["B11"];

                if (_dr[EnumToInt(TcfHeader.REGCUSTOM)].ToString().Contains("B1O")
                || _dr[EnumToInt(TcfHeader.REGCUSTOM)].ToString().Contains("B3O"))
                {
                    intTx04 |= DicFactor.Tx04["B1"];
                }
                else if( _dr[EnumToInt(TcfHeader.REGCUSTOM)].ToString().Contains("B41O"))
                {
                    int TempintTx04 = 0; 

                    if (_dr[EnumToInt(TcfHeader.Note)].ToString().Contains("EU")|| _dr[EnumToInt(TcfHeader.Note)].ToString().Contains("ONLY"))
                    {
                        TempintTx04 = DicFactor.Tx04["B1"];
                    }
                    else if (_dr[EnumToInt(TcfHeader.Note)].ToString().Contains("US"))
                    {
                        TempintTx04 = DicFactor.Tx04["B25"];
                    }

                    intTx04 |= TempintTx04;

                }
                else
                {
                    intTx04 |= 0x0C;
                }
            }
            else
            {
                intTx04 = DicFactor.Tx04[_dr[EnumToInt(TcfHeader.BAND)].ToString()];

                if (_dr[EnumToInt(TcfHeader.Note)].ToString().Contains("EU"))
                {
                    intTx04 = DicFactor.Tx04["B1"];
                }
                else if (_dr[EnumToInt(TcfHeader.Note)].ToString().Contains("US"))
                {
                    intTx04 = DicFactor.Tx04["B25"];
                }

                intTx04 |= 0xC0;
            }
            
            //Tx06
            int intTx06 = 0;
            intTx06 = DicFactor.Tx06[NoteArry[0]];
            intTx06 |= DicFactor.Tx06[NoteArry[1]];
           
            int intTx07 = 0, intTx08=0, intTx09 = 0, intRx00 =0, intRx01=0, intRx02=0, intRx03=0, intRx04=0, 
                intRx0B=0, intRx0D=0, intRx0F=0, intRx11=0, intRx13=0;

            foreach (string _temp in LNAArry)
            {
                string _tband = _temp.Split('O')[0];
                string _tMode = "G"+ _temp.Split('G')[1];
                string _tOUT = _temp.Split('G')[0];
                _tOUT = "OUT" + _tOUT.Substring(_tOUT.Length - 1, 1);

                intTx07 |= DicFactor.Tx07[_tband];
                intTx08 |= DicFactor.Tx08[_tband];
                intTx09 |= DicFactor.Tx09[_tband];
                intRx00 |= DicFactor.Rx00[_tband];
                intRx01 |= DicFactor.Rx01[_tband];
                intRx02 |= DicFactor.Rx02[_tband];
                intRx03 |= DicFactor.Rx03[_tband+ _tOUT];
                intRx04 |= DicFactor.Rx04[_tband + _tOUT];

                intRx0B |= DicFactor.Rx0B[_tband + _tMode ];
                intRx0D |= DicFactor.Rx0D[_tband + _tMode];
                intRx0F |= DicFactor.Rx0F[_tband + _tMode];
                intRx11 |= DicFactor.Rx11[_tband + _tMode];
                intRx13 |= DicFactor.Rx13[_tband + _tMode];
            }


            _dr[EnumToInt(TcfHeader.Tx4)] = intTx04.ToString("X");
            _dr[EnumToInt(TcfHeader.Tx5)] = "0C";
            _dr[EnumToInt(TcfHeader.Tx6)] = intTx06.ToString("X");
            _dr[EnumToInt(TcfHeader.Tx7)] = intTx07.ToString("X");
            _dr[EnumToInt(TcfHeader.Tx8)] = intTx08.ToString("X");
            _dr[EnumToInt(TcfHeader.Tx9)] = intTx09.ToString("X");
            _dr[EnumToInt(TcfHeader.Rx0)] = intRx00.ToString("X");
            _dr[EnumToInt(TcfHeader.Rx1)] = intRx01.ToString("X");
            _dr[EnumToInt(TcfHeader.Rx2)] = intRx02.ToString("X");
            _dr[EnumToInt(TcfHeader.Rx3)] = intRx03.ToString("X");
            _dr[EnumToInt(TcfHeader.Rx4)] = intRx04.ToString("X");

            _dr[EnumToInt(TcfHeader.RxB)] = intRx0B.ToString("X");
            _dr[EnumToInt(TcfHeader.RxD)] = intRx0D.ToString("X");
            _dr[EnumToInt(TcfHeader.RxF)] = intRx0F.ToString("X");
            _dr[EnumToInt(TcfHeader.Rx11)] = intRx11.ToString("X");
            _dr[EnumToInt(TcfHeader.Rx13)] = intRx13.ToString("X");


            _dr[EnumToInt(TcfHeader.RXLNA1)] = intRx0B.ToString("X");
            _dr[EnumToInt(TcfHeader.RXLNA2)] = intRx0D.ToString("X");
            _dr[EnumToInt(TcfHeader.RXLNA3)] = intRx0F.ToString("X");
            _dr[EnumToInt(TcfHeader.RXLNA4)] = intRx11.ToString("X");
            _dr[EnumToInt(TcfHeader.RXLNA5)] = intRx13.ToString("X");


            _dr[EnumToInt(TcfHeader.MIPI_Commands)]
                = "3(0x1C:0x40)(0x1C:0x38)(0x2D:0xFF)(0x0:0x0)"
                + "(0x4:0x" + intTx04.ToString("X") + ")"
                + "(0x5:0x0C)"
                + "(0x6:0x" + intTx06.ToString("X") + ")"
                + "(0x7:0x" + intTx07.ToString("X") + ")"
                + "(0x8:0x" + intTx08.ToString("X") + ")"
                + "(0x9:0x" + intTx09.ToString("X") + ")"
                + "(0xB:0x00)(0xC:0x00)(0xD:0x00)"
                + "3(0x1C:0x40)(0x1C:0x38)(0x2D:0xFF)"
                + "(0x0:0x" + intRx00.ToString("X") + ")"
                + "(0x1:0x" + intRx01.ToString("X") + ")"
                + "(0x2:0x" + intRx02.ToString("X") + ")"
                + "(0x3:0x" + intRx03.ToString("X") + ")"
                + "(0x4:0x" + intRx04.ToString("X") + ")"
                + "(0xB:0x" + intRx0B.ToString("X") + ")"
                + "(0xD:0x" + intRx0D.ToString("X") + ")"
                + "(0xF:0x" + intRx0F.ToString("X") + ")"
                + "(0x11:0x" + intRx11.ToString("X") + ")"
                + "(0x13:0x" + intRx13.ToString("X") + ")";

            _dr[EnumToInt(TcfHeader.TRX)] = "RX";
            _dr[EnumToInt(TcfHeader.TXDAQ1)] = "0";
            _dr[EnumToInt(TcfHeader.TXDAQ2)] = "0";            

            return _dr;
        }

        private void ExportCVSfromDataTable(DataTable dt)
        {
            IEnumerable<string> columnNames = dt.Columns.Cast<DataColumn>().
                                  Select(column => column.ColumnName);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Join(",", columnNames));
            foreach (DataRow row in dt.Rows)
            {
                string[] fields = row.ItemArray.Select(field => field.ToString()).ToArray();
                sb.AppendLine(string.Join(",", fields));
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    File.WriteAllText(sfd.FileName, sb.ToString());
                }
            }
        }

        private class CAcombination
        {
            public string Type;
            public Dictionary<string, string> BandPort = new Dictionary<string, string>();
            public string MuxCase;
            public string Swplex;
        }
        
        private int EnumToInt(Enum _TcfEnum)
        {
            object enumObject = _TcfEnum;
            return (int)enumObject;
        }
        
        #region Enum properties
        enum TcfHeader
        {
            Type = 0,
            BAND,           
            Switch_TX, Switch_ANT, Switch_RX,
            Freq, Pout, Pin,TRX,
            Power_Mode,
            MIPI_Commands, 
            TXDAQ1, TXDAQ2,
            RXLNA1, RXLNA2, RXLNA3, RXLNA4, RXLNA5,
            REGCUSTOM, Note, SwPlex, ExpectedGain,
            Tx0, Tx3, Tx4, Tx5, Tx6, Tx7, Tx8, Tx9, TxB, TxC, TxD, 
            Rx0, Rx1, Rx2, Rx3, Rx4, RxB, RxD, RxF, Rx11, Rx13
            
        }
        enum Type
        {
            Alone,
            CA2,
            CA3,
            CA4
        }
        enum MuxCase
        {
            Iso,
            B7,
            B41,
        }
        enum eMode
        {
            G0,G1,G2,G3,G4,G5,G6
        }
        enum eBand
        {
            B1,B66,B34,
            B39,B3,B25,
            B41,B41H,B7,
            B30,B40,B40A,
            B32,B11,B21
        }

        #endregion

        public class cDictionalyfactory
        {
            public Dictionary<string, double> dicFreq = new Dictionary<string, double>();

            public Dictionary<string, int> Tx04 = new Dictionary<string, int>();
            public Dictionary<string, int> Tx05 = new Dictionary<string, int>();
            public Dictionary<string, int> Tx06 = new Dictionary<string, int>();
            public Dictionary<string, int> Tx07= new Dictionary<string, int>();
            public Dictionary<string, int> Tx08 = new Dictionary<string, int>();
            public Dictionary<string, int> Tx09 = new Dictionary<string, int>();

            public Dictionary<string, int> Rx00 = new Dictionary<string, int>();
            public Dictionary<string, int> Rx01 = new Dictionary<string, int>();
            public Dictionary<string, int> Rx02 = new Dictionary<string, int>();
            public Dictionary<string, int> Rx03 = new Dictionary<string, int>();
            public Dictionary<string, int> Rx04 = new Dictionary<string, int>();
            public Dictionary<string, int> Rx0B = new Dictionary<string, int>();
            public Dictionary<string, int> Rx0D = new Dictionary<string, int>();
            public Dictionary<string, int> Rx0F = new Dictionary<string, int>();
            public Dictionary<string, int> Rx11 = new Dictionary<string, int>();
            public Dictionary<string, int> Rx13 = new Dictionary<string, int>();

            public cDictionalyfactory()
            {
                //Band
                dicFreq.Add("B1", 2140);
                dicFreq.Add("B25", 1962.5);
                dicFreq.Add("B3", 1842.5);
                dicFreq.Add("B66", 2155);
                dicFreq.Add("B34", 2017.5);
                dicFreq.Add("B39", 1900);
                dicFreq.Add("B7", 2655);
                dicFreq.Add("B30", 2355);
                dicFreq.Add("B40", 2350);
                dicFreq.Add("B40A", 2335);
                dicFreq.Add("B41", 2593);
                dicFreq.Add("B41H", 2593);
                dicFreq.Add("B32", 1474);
                dicFreq.Add("B11", 1493.4);
                dicFreq.Add("B21", 1503.4);

                //ANT1      // B7 & B41 default EU
                Tx04.Add("B1", 0x01);
                Tx04.Add("B25", 0x02);
                Tx04.Add("B3", 0x01);
                Tx04.Add("B66", 0x02);
                Tx04.Add("B34", 0x04);
                Tx04.Add("B39", 0x04);
                Tx04.Add("B7", 0x01);
                Tx04.Add("B30", 0x02);
                Tx04.Add("B40", 0x01);
                Tx04.Add("B40A", 0x05);
                Tx04.Add("B41", 0x01);
                Tx04.Add("B41H", 0x04);
                Tx04.Add("B32", 0x01);
                Tx04.Add("B11", 0x90);
                Tx04.Add("B21", 0x90);

                
                //Mux
                Tx06.Add("ONLY", 0x0);
                Tx06.Add("US", 0x1);
                Tx06.Add("EU", 0x2);
                Tx06.Add("B7", 0x10);
                Tx06.Add("B41", 0x20);
                Tx06.Add("ISO", 0);
                Tx06.Add("x", 0);


                //MB Rx SW 
                Tx07.Add("B1", 0);
                Tx07.Add("B25", 0);
                Tx07.Add("B3", 0);
                Tx07.Add("B66", 0);
                Tx07.Add("B34", 0x10);
                Tx07.Add("B39", 0x80);
                Tx07.Add("B7", 0);
                Tx07.Add("B30", 0);
                Tx07.Add("B40", 0);
                Tx07.Add("B40A", 0);
                Tx07.Add("B41", 0);
                Tx07.Add("B41H", 0);
                Tx07.Add("B32", 0);
                Tx07.Add("B11", 0);
                Tx07.Add("B21", 0);


                //MB Rx SW 
                Tx08.Add("B1", 0);
                Tx08.Add("B25", 0);
                Tx08.Add("B3", 0);
                Tx08.Add("B66", 0);
                Tx08.Add("B34", 0);
                Tx08.Add("B39", 0);
                Tx08.Add("B7", 0);
                Tx08.Add("B30", 0);
                Tx08.Add("B40", 0x80);
                Tx08.Add("B40A",0x40);
                Tx08.Add("B41", 0x10);
                Tx08.Add("B41H", 0x20);
                Tx08.Add("B32", 0);
                Tx08.Add("B11", 0);
                Tx08.Add("B21", 0);

                //LMB Rx SW 
                Tx09.Add("B1", 0);
                Tx09.Add("B25", 0);
                Tx09.Add("B3", 0);
                Tx09.Add("B66", 0);
                Tx09.Add("B34", 0);
                Tx09.Add("B39", 0);
                Tx09.Add("B7", 0);
                Tx09.Add("B30", 0);
                Tx09.Add("B40", 0);
                Tx09.Add("B40A", 0);
                Tx09.Add("B41", 0);
                Tx09.Add("B41H", 0);
                Tx09.Add("B32", 0);
                Tx09.Add("B11", 0x0);
                Tx09.Add("B21", 0x0);



                //Rx HB select
                Rx00.Add("B1", 0);
                Rx00.Add("B25", 0);
                Rx00.Add("B3", 0);
                Rx00.Add("B66", 0);
                Rx00.Add("B34", 0);
                Rx00.Add("B39", 0);
                Rx00.Add("B7", 0x3);
                Rx00.Add("B30", 0x8);
                Rx00.Add("B40", 0x10);
                Rx00.Add("B40A", 0x18);
                Rx00.Add("B41", 0x1);
                Rx00.Add("B41H", 0x2);
                Rx00.Add("B32", 0);
                Rx00.Add("B11", 0);
                Rx00.Add("B21", 0);


                //Rx MB select 
                Rx01.Add("B1",0x1);
                Rx01.Add("B25", 0x8);
                Rx01.Add("B3", 0x10);
                Rx01.Add("B66", 0x2);
                Rx01.Add("B34", 0x3);
                Rx01.Add("B39", 0x18);
                Rx01.Add("B7", 0);
                Rx01.Add("B30", 0);
                Rx01.Add("B40", 0);
                Rx01.Add("B40A", 0);
                Rx01.Add("B41", 0);
                Rx01.Add("B41H", 0);
                Rx01.Add("B32", 0);
                Rx01.Add("B11", 0);
                Rx01.Add("B21", 0);

                //Rx MB select 
                Rx02.Add("B1", 0);
                Rx02.Add("B25", 0);
                Rx02.Add("B3", 0);
                Rx02.Add("B66", 0);
                Rx02.Add("B34", 0);
                Rx02.Add("B39", 0);
                Rx02.Add("B7", 0);
                Rx02.Add("B30", 0);
                Rx02.Add("B40", 0);
                Rx02.Add("B40A", 0);
                Rx02.Add("B41", 0);
                Rx02.Add("B41H", 0);
                Rx02.Add("B32", 0x1);
                Rx02.Add("B11", 0x2);
                Rx02.Add("B21", 0x3);

                //LNA OUT Sw
                Rx03.Add("B25OUT1", 0x1);
                Rx03.Add("B3OUT1", 0x1);
                Rx03.Add("B39OUT1", 0x1);

                Rx03.Add("B30OUT1", 0x2);
                Rx03.Add("B40OUT1", 0x2);
                Rx03.Add("B40AOUT1", 0x2);

                Rx03.Add("B1OUT1", 0x3);
                Rx03.Add("B66OUT1", 0x3);
                Rx03.Add("B34OUT1", 0x3);
                
                Rx03.Add("B25OUT2", 0x8);
                Rx03.Add("B3OUT2", 0x8);
                Rx03.Add("B39OUT2", 0x8);
                
                Rx03.Add("B30OUT2", 0x10);
                Rx03.Add("B40OUT2", 0x10);
                Rx03.Add("B40AOUT2", 0x10);
                                
                Rx03.Add("B1OUT2", 0x18);
                Rx03.Add("B66OUT2", 0x18);
                Rx03.Add("B34OUT2", 0x18);


                Rx03.Add("B1OUT3", 0);
                Rx03.Add("B66OUT3", 0);
                Rx03.Add("B34OUT3", 0);

                Rx03.Add("B7OUT3", 0);
                Rx03.Add("B41OUT3", 0);
                Rx03.Add("B41HOUT3", 0);

                Rx03.Add("B25OUT3", 0);
                Rx03.Add("B3OUT3", 0);
                Rx03.Add("B39OUT3", 0);
                
                Rx03.Add("B7OUT4", 0);
                Rx03.Add("B41OUT4", 0);
                Rx03.Add("B41HOUT4", 0);

                Rx03.Add("B32OUT4", 0);
                Rx03.Add("B11OUT4", 0);
                Rx03.Add("B21OUT4", 0);
                                                        
                Rx03.Add("B25OUT4", 0);
                Rx03.Add("B3OUT4", 0);

                Rx03.Add("B1OUT4", 0);
                Rx03.Add("B66OUT4", 0);                
                Rx03.Add("B34OUT4", 0);

                //LNA OUT Sw
                Rx04.Add("B25OUT1", 0);
                Rx04.Add("B3OUT1", 0);
                Rx04.Add("B39OUT1", 0);

                Rx04.Add("B30OUT1", 0);
                Rx04.Add("B40OUT1", 0);
                Rx04.Add("B40AOUT1", 0);

                Rx04.Add("B1OUT1", 0);
                Rx04.Add("B66OUT1", 0);
                Rx04.Add("B34OUT1", 0);

                Rx04.Add("B25OUT2", 0);
                Rx04.Add("B3OUT2", 0);
                Rx04.Add("B39OUT2", 0);

                Rx04.Add("B30OUT2", 0);
                Rx04.Add("B40OUT2", 0);
                Rx04.Add("B40AOUT2", 0);

                Rx04.Add("B1OUT2", 0);
                Rx04.Add("B66OUT2", 0);
                Rx04.Add("B34OUT2", 0);


                Rx04.Add("B1OUT3", 0x1);
                Rx04.Add("B66OUT3", 0x1);
                Rx04.Add("B34OUT3", 0x1);

                Rx04.Add("B7OUT3", 0x2);
                Rx04.Add("B41OUT3", 0x2);
                Rx04.Add("B41HOUT3", 0x2);

                Rx04.Add("B25OUT3", 0x3);
                Rx04.Add("B3OUT3", 0x3);
                Rx04.Add("B39OUT3", 0x3);

                Rx04.Add("B7OUT4", 0x8);
                Rx04.Add("B41OUT4", 0x8);
                Rx04.Add("B41HOUT4", 0x8);

                Rx04.Add("B32OUT4", 0x10);
                Rx04.Add("B11OUT4", 0x10);
                Rx04.Add("B21OUT4", 0x10);

                Rx04.Add("B25OUT4", 0x18);
                Rx04.Add("B3OUT4", 0x18);

                Rx04.Add("B1OUT4", 0x20);
                Rx04.Add("B66OUT4", 0x20);
                Rx04.Add("B34OUT4", 0x20);

                Dictionary<string, int> mode = new Dictionary<string, int>();

                mode.Add("G0", 0x40);
                mode.Add("G1", 0x48);
                mode.Add("G2", 0x50);
                mode.Add("G3", 0x58);
                mode.Add("G4", 0x60);
                mode.Add("G5", 0x68);
                mode.Add("G6", 0x70);

                foreach (string _mode in Enum.GetNames(typeof(eMode)))
                {
                    foreach (string _Band in Enum.GetNames(typeof(eBand)))
                    {
                        if (_Band == "B1" || _Band == "B66" || _Band == "B34")
                        {
                            Rx0B.Add(_Band + _mode, mode[_mode]);
                            Rx0D.Add(_Band + _mode, 0);
                            Rx0F.Add(_Band + _mode, 0);
                            Rx11.Add(_Band + _mode, 0);
                            Rx13.Add(_Band + _mode, 0);
                        }
                        else if (_Band == "B39" || _Band == "B3" || _Band == "B25")
                        {
                            Rx0B.Add(_Band + _mode,0);
                            Rx0D.Add(_Band + _mode, mode[_mode]);
                            Rx0F.Add(_Band + _mode, 0);
                            Rx11.Add(_Band + _mode, 0);
                            Rx13.Add(_Band + _mode, 0);
                        }
                        else if (_Band == "B41" || _Band == "B41H" || _Band == "B7")
                        {
                            Rx0B.Add(_Band + _mode, 0);
                            Rx0D.Add(_Band + _mode, 0);
                            Rx0F.Add(_Band + _mode, mode[_mode]);
                            Rx11.Add(_Band + _mode, 0);
                            Rx13.Add(_Band + _mode, 0);
                        }
                        else if (_Band == "B30" || _Band == "B40" || _Band == "B40A")
                        {
                            Rx0B.Add(_Band + _mode, 0);
                            Rx0D.Add(_Band + _mode, 0);
                            Rx0F.Add(_Band + _mode, 0);
                            Rx11.Add(_Band + _mode, mode[_mode]);
                            Rx13.Add(_Band + _mode, 0);
                        }
                        else if (_Band == "B32" || _Band == "B11" || _Band == "B21")
                        {
                            Rx0B.Add(_Band + _mode, 0);
                            Rx0D.Add(_Band + _mode, 0);
                            Rx0F.Add(_Band + _mode, 0);
                            Rx11.Add(_Band + _mode, 0);
                            Rx13.Add(_Band + _mode, mode[_mode]);
                        }

                    }
                }

            }


        }
    }
}

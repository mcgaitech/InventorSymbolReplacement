using System.ComponentModel;

namespace MCGInventorPlugin.Models.SymbolHandler
{
    /// <summary>
    /// Đại diện cho một prompt field (attribute) của SketchedSymbol.
    /// Hiển thị trên DataGrid trong Properties panel — user có thể edit Value trực tiếp.
    /// </summary>
    public class PromptFieldModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Tên field (từ TextBox.Text trong definition sketch).</summary>
        public string Name { get; set; }

        private string _value = string.Empty;

        /// <summary>Giá trị hiện tại — user edit trực tiếp trên DataGrid.</summary>
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        /// <summary>Index của TextBox trong definition (dùng để match khi BuildPromptStrings).</summary>
        public int TextBoxIndex { get; set; }
    }
}

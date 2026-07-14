using System.Windows.Controls;

using Worksheet.App.Models;
namespace Worksheet.App.Models
{
    public interface IWorksheetItem
    {
        Canvas Container { get; }
        double Width { get; }
        double Height { get; }
    }
}

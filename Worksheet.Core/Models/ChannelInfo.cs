namespace Worksheet.Core.Models
{
    public class ChannelInfo
    {
        public int Id { get; set; }
        public string DaqChannel { get; set; }
        public string Wavelength { get; set; }
        public string ExportedName { get; set; }

        public ChannelInfo(int id, string daqChannel, string wavelength, string? exportedName = null)
        {
            Id = id;
            DaqChannel = daqChannel;
            Wavelength = wavelength;
            ExportedName = exportedName ?? wavelength;
        }
    }
}

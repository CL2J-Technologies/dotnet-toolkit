using System.Diagnostics;
using System.Threading.Tasks;

namespace cl2j.DataStore.TestApp
{
    internal class DataStoreSample
    {
        private readonly IDataStore<string, Color> dataStoreColorRead;

        public DataStoreSample(IDataStore<string, Color> dataStoreColorRead)
        {
            this.dataStoreColorRead = dataStoreColorRead;
        }

        public async Task ExecuteAsync()
        {
            //Create colors
            await dataStoreColorRead.InsertAsync(new Color { Id = "red", Value = "#f00" });
            await dataStoreColorRead.InsertAsync(new Color { Id = "green", Value = "#0f0" });
            await dataStoreColorRead.InsertAsync(new Color { Id = "blue", Value = "#00f" });
            await dataStoreColorRead.InsertAsync(new Color { Id = "cyan", Value = "#0ff" });
            await dataStoreColorRead.InsertAsync(new Color { Id = "magenta", Value = "#f0f" });
            await dataStoreColorRead.InsertAsync(new Color { Id = "yellow", Value = "#ff0" });
            await dataStoreColorRead.InsertAsync(new Color { Id = "black", Value = "#000" });

            //Retreive all the colors
            var colors = await dataStoreColorRead.GetAllAsync();
            foreach (var color in colors)
                Debug.WriteLine($"{color.Id} -> {color.Value}");

            //Retreive the color black and update it's value
            var black = await dataStoreColorRead.GetByIdAsync("black");
            if (black != null)
            {
                black.Value = "#111";
                await dataStoreColorRead.UpdateAsync(black);
            }

            //Delete a color
            await dataStoreColorRead.DeleteAsync("magenta");
        }
    }
}
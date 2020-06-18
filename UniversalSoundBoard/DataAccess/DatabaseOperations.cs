﻿using davClassLibrary;
using davClassLibrary.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UniversalSoundboard.Models;
using UniversalSoundBoard.Models;
using Windows.Storage;

namespace UniversalSoundBoard.DataAccess
{
    public class DatabaseOperations
    {
        #region General Methods
        public static async Task<TableObject> GetTableObjectAsync(Guid uuid)
        {
            return await Dav.Database.GetTableObjectAsync(uuid);
        }

        public static async Task<List<TableObject>> GetTableObjectsByPropertyAsync(string propertyName, string propertyValue)
        {
            return await Dav.Database.GetTableObjectsByPropertyAsync(propertyName, propertyValue);
        }

        public static async Task<bool> TableObjectExistsAsync(Guid uuid)
        {
            return await Dav.Database.TableObjectExistsAsync(uuid);
        }

        public static async Task DeleteTableObjectAsync(Guid uuid)
        {
            // Get the object and delete it
            var tableObject = await Dav.Database.GetTableObjectAsync(uuid);
            if (tableObject != null)
                await tableObject.DeleteAsync();
        }
        #endregion

        #region Sound
        public static async Task<TableObject> CreateSoundAsync(Guid uuid, string name, bool favourite, Guid soundUuid, List<Guid> categoryUuids)
        {
            // Create TableObject with sound informations and TableObject with the Soundfile
            var properties = new List<Property>
            {
                new Property{ Name = FileManager.SoundTableNamePropertyName, Value = name },
                new Property{ Name = FileManager.SoundTableFavouritePropertyName, Value = favourite.ToString() },
                new Property{ Name = FileManager.SoundTableSoundUuidPropertyName, Value = soundUuid.ToString() }
            };

            if (categoryUuids != null)
                properties.Add(new Property { Name = FileManager.SoundTableCategoryUuidPropertyName, Value = string.Join(",", categoryUuids) });

            return await TableObject.CreateAsync(uuid, FileManager.SoundTableId, properties);
        }

        public static async Task<List<TableObject>> GetAllSoundsAsync()
        {
            return await Dav.Database.GetAllTableObjectsAsync(FileManager.SoundTableId, false);
        }

        public static async Task UpdateSoundAsync(Guid uuid, string name, bool? favourite, Guid? imageUuid, List<Guid> categoryUuids)
        {
            // Get the sound table object
            var soundTableObject = await Dav.Database.GetTableObjectAsync(uuid);
            if (soundTableObject == null || soundTableObject.TableId != FileManager.SoundTableId) return;

            if (!string.IsNullOrEmpty(name))
                await soundTableObject.SetPropertyValueAsync(FileManager.SoundTableNamePropertyName, name);
            if (favourite.HasValue)
                await soundTableObject.SetPropertyValueAsync(FileManager.SoundTableFavouritePropertyName, favourite.Value.ToString());
            if (imageUuid.HasValue)
                await soundTableObject.SetPropertyValueAsync(FileManager.SoundTableImageUuidPropertyName, imageUuid.Value.ToString());
            if (categoryUuids != null)
                await soundTableObject.SetPropertyValueAsync(FileManager.SoundTableCategoryUuidPropertyName, string.Join(",", categoryUuids));
        }
        
        public static async Task DeleteSoundAsync(Guid uuid)
        {
            var soundTableObject = await Dav.Database.GetTableObjectAsync(uuid);
            if (soundTableObject == null || soundTableObject.TableId != FileManager.SoundTableId) return;

            // Delete the sound file and the image file
            Guid? soundFileUuid = FileManager.ConvertStringToGuid(soundTableObject.GetPropertyValue(FileManager.SoundTableSoundUuidPropertyName));
            Guid? imageFileUuid = FileManager.ConvertStringToGuid(soundTableObject.GetPropertyValue(FileManager.SoundTableImageUuidPropertyName));

            if (soundFileUuid.HasValue && !Equals(soundFileUuid, Guid.Empty))
            {
                var soundFileTableObject = await Dav.Database.GetTableObjectAsync(soundFileUuid.Value);
                if (soundFileTableObject != null)
                    await soundFileTableObject.DeleteAsync();
            }
            if (imageFileUuid.HasValue && !Equals(imageFileUuid, Guid.Empty))
            {
                var imageFileTableObject = await Dav.Database.GetTableObjectAsync(imageFileUuid.Value);
                if (imageFileTableObject != null)
                    await imageFileTableObject.DeleteAsync();
            }

            // Delete the sound itself
            await soundTableObject.DeleteAsync();
        }
        #endregion

        #region SoundFile
        public static async Task<TableObject> CreateSoundFileAsync(Guid uuid, StorageFile audioFile)
        {
            return await TableObject.CreateAsync(uuid, FileManager.SoundFileTableId, new FileInfo(audioFile.Path));
        }

        public static async Task<List<TableObject>> GetAllSoundFilesAsync()
        {
            return await Dav.Database.GetAllTableObjectsAsync(FileManager.SoundFileTableId, false);
        }
        #endregion SoundFile

        #region ImageFile
        public static async Task<TableObject> CreateImageFileAsync(Guid uuid, StorageFile imageFile)
        {
            return await TableObject.CreateAsync(uuid, FileManager.ImageFileTableId, new FileInfo(imageFile.Path));
        }

        public static async Task UpdateImageFileAsync(Guid uuid, StorageFile imageFile)
        {
            var imageFileTableObject = await Dav.Database.GetTableObjectAsync(uuid);
            if (imageFileTableObject == null || imageFileTableObject.TableId != FileManager.ImageFileTableId) return;

            await imageFileTableObject.SetFileAsync(new FileInfo(imageFile.Path));
        }
        #endregion

        #region Category
        public static async Task<TableObject> CreateCategoryAsync(Guid uuid, Guid? parent, string name, string icon)
        {
            List<Property> properties = new List<Property>
            {
                new Property{ Name = FileManager.CategoryTableNamePropertyName, Value = name },
                new Property{ Name = FileManager.CategoryTableIconPropertyName, Value = icon }
            };

            if (parent.HasValue)
                properties.Add(new Property { Name = FileManager.CategoryTableParentPropertyName, Value = parent.Value.ToString() });

            return await TableObject.CreateAsync(uuid, FileManager.CategoryTableId, properties);
        }

        public static async Task<List<TableObject>> GetAllCategoriesAsync()
        {
            return await Dav.Database.GetAllTableObjectsAsync(FileManager.CategoryTableId, false);
        }

        public static async Task UpdateCategoryAsync(Guid uuid, string name, string icon)
        {
            var categoryTableObject = await Dav.Database.GetTableObjectAsync(uuid);
            if (categoryTableObject == null || categoryTableObject.TableId != FileManager.CategoryTableId) return;

            if (!string.IsNullOrEmpty(name))
                await categoryTableObject.SetPropertyValueAsync(FileManager.CategoryTableNamePropertyName, name);
            if (!string.IsNullOrEmpty(icon))
                await categoryTableObject.SetPropertyValueAsync(FileManager.CategoryTableIconPropertyName, icon);
        }

        public static async Task DeleteCategoryAsync(Guid uuid)
        {
            var categoryTableObject = await Dav.Database.GetTableObjectAsync(uuid);
            if (categoryTableObject == null || categoryTableObject.TableId != FileManager.CategoryTableId) return;

            await categoryTableObject.DeleteAsync();
        }
        #endregion

        #region PlayingSound
        public static async Task<TableObject> CreatePlayingSoundAsync(Guid uuid, List<Guid> soundUuids, int current, int repetitions, bool randomly, double volume)
        {
            var properties = new List<Property>
            {
                new Property{ Name = FileManager.PlayingSoundTableSoundIdsPropertyName, Value = string.Join(",", soundUuids) },
                new Property{ Name = FileManager.PlayingSoundTableCurrentPropertyName, Value = current.ToString() },
                new Property{ Name = FileManager.PlayingSoundTableRepetitionsPropertyName, Value = repetitions.ToString() },
                new Property{ Name = FileManager.PlayingSoundTableRandomlyPropertyName, Value = randomly.ToString() },
                new Property{ Name = FileManager.PlayingSoundTableVolumePropertyName, Value = volume.ToString() }
            };

            return await TableObject.CreateAsync(uuid, FileManager.PlayingSoundTableId, properties);
        }

        public static async Task<List<TableObject>> GetAllPlayingSoundsAsync()
        {
            return await Dav.Database.GetAllTableObjectsAsync(FileManager.PlayingSoundTableId, false);
        }

        public static async Task<TableObject> GetPlayingSoundAsync(Guid uuid)
        {
            var tableObject = await Dav.Database.GetTableObjectAsync(uuid);
            if (tableObject == null || tableObject.TableId != FileManager.PlayingSoundTableId) return null;

            return tableObject;
        }

        public static async Task UpdatePlayingSoundAsync(Guid uuid, List<Guid> soundUuids, int? current, int? repetitions, bool? randomly, double? volume)
        {
            var playingSoundTableObject = await Dav.Database.GetTableObjectAsync(uuid);
            if (playingSoundTableObject == null || playingSoundTableObject.TableId != FileManager.PlayingSoundTableId) return;

            if (soundUuids != null)
                await playingSoundTableObject.SetPropertyValueAsync(FileManager.PlayingSoundTableSoundIdsPropertyName, string.Join(",", soundUuids));
            if (current.HasValue)
                await playingSoundTableObject.SetPropertyValueAsync(FileManager.PlayingSoundTableCurrentPropertyName, current.Value.ToString());
            if (repetitions.HasValue)
                await playingSoundTableObject.SetPropertyValueAsync(FileManager.PlayingSoundTableRepetitionsPropertyName, repetitions.Value.ToString());
            if (randomly.HasValue)
                await playingSoundTableObject.SetPropertyValueAsync(FileManager.PlayingSoundTableRandomlyPropertyName, randomly.Value.ToString());
            if (volume.HasValue)
                await playingSoundTableObject.SetPropertyValueAsync(FileManager.PlayingSoundTableVolumePropertyName, volume.Value.ToString());
        }

        public static async Task DeletePlayingSound(Guid uuid)
        {
            var playingSoundTableObject = await Dav.Database.GetTableObjectAsync(uuid);
            if (playingSoundTableObject == null || playingSoundTableObject.TableId != FileManager.PlayingSoundTableId) return;

            await playingSoundTableObject.DeleteAsync();
        }
        #endregion

        #region Order
        public static async Task<List<TableObject>> GetAllOrdersAsync()
        {
            return await Dav.Database.GetAllTableObjectsAsync(FileManager.OrderTableId, false);
        }
        #endregion

        #region CategoryOrder
        public static async Task SetCategoryOrderAsync(List<Guid> uuids)
        {
            // Check if the order already exists
            List<TableObject> tableObjects = await GetAllOrdersAsync();
            TableObject tableObject = tableObjects.Find(obj => obj.GetPropertyValue(FileManager.OrderTableTypePropertyName) == FileManager.CategoryOrderType);

            if (tableObject == null)
            {
                // Create a new table object
                List<Property> properties = new List<Property>
                {
                    // Set the type property
                    new Property { Name = FileManager.OrderTableTypePropertyName, Value = FileManager.CategoryOrderType }
                };

                int i = 0;
                foreach (var uuid in uuids)
                {
                    properties.Add(new Property { Name = i.ToString(), Value = uuid.ToString() });
                    i++;
                }

                await TableObject.CreateAsync(Guid.NewGuid(), FileManager.OrderTableId, properties);
            }
            else
            {
                // Update the existing object
                int i = 0;
                Dictionary<string, string> newProperties = new Dictionary<string, string>();
                foreach(var uuid in uuids)
                {
                    newProperties.Add(i.ToString(), uuid.ToString());
                    i++;
                }
                await tableObject.SetPropertyValuesAsync(newProperties);

                // Remove the properties that are outside of the uuids range
                List<string> removedProperties = new List<string>();
                foreach(var property in tableObject.Properties)
                    if (int.TryParse(property.Name, out int propertyIndex) && propertyIndex >= uuids.Count)
                        removedProperties.Add(property.Name);

                for (int j = 0; j < removedProperties.Count; j++)
                    await tableObject.RemovePropertyAsync(removedProperties[j]);
            }
        }
        #endregion

        #region SoundOrder
        public static async Task SetSoundOrderAsync(Guid categoryUuid, bool favourite, List<Guid> uuids)
        {
            // Check if the order object already exists
            List<TableObject> tableObjects = await GetAllOrdersAsync();
            TableObject tableObject = tableObjects.Find((TableObject obj) => {
                // Check if the object is of type Sound
                if (obj.GetPropertyValue(FileManager.OrderTableTypePropertyName) != FileManager.SoundOrderType) return false;

                // Check if the object has the right category uuid
                string categoryUuidString = obj.GetPropertyValue(FileManager.OrderTableCategoryPropertyName);
                Guid? cUuid = FileManager.ConvertStringToGuid(categoryUuidString);
                if (!cUuid.HasValue) return false;

                string favString = obj.GetPropertyValue(FileManager.OrderTableFavouritePropertyName);
                bool.TryParse(favString, out bool fav);

                return Equals(categoryUuid, cUuid) && favourite == fav;
            });

            if(tableObject == null)
            {
                // Create a new table object
                List<Property> properties = new List<Property>
                {
                    // Set the type property
                    new Property { Name = FileManager.OrderTableTypePropertyName, Value = FileManager.SoundOrderType },
                    // Set the category property
                    new Property { Name = FileManager.OrderTableCategoryPropertyName, Value = categoryUuid.ToString() },
                    // Set the favourite property
                    new Property { Name = FileManager.OrderTableFavouritePropertyName, Value = favourite.ToString() }
                };

                int i = 0;
                foreach (var uuid in uuids)
                {
                    properties.Add(new Property { Name = i.ToString(), Value = uuid.ToString() });
                    i++;
                }

                await TableObject.CreateAsync(Guid.NewGuid(), FileManager.OrderTableId, properties);
            }
            else
            {
                // Update the existing object
                int i = 0;
                Dictionary<string, string> newProperties = new Dictionary<string, string>();
                foreach (var uuid in uuids)
                {
                    newProperties.Add(i.ToString(), uuid.ToString());
                    i++;
                }
                await tableObject.SetPropertyValuesAsync(newProperties);
                
                bool removeNonExistentSounds = FileManager.itemViewHolder.User == null || !FileManager.itemViewHolder.User.IsLoggedIn ||
                                                (FileManager.itemViewHolder.User.IsLoggedIn && FileManager.syncFinished);

                if (removeNonExistentSounds)
                {
                    // Remove the properties that are outside of the uuids range
                    List<string> removedProperties = new List<string>();
                    foreach (var property in tableObject.Properties)
                        if (int.TryParse(property.Name, out int propertyIndex) && propertyIndex >= uuids.Count)
                            removedProperties.Add(property.Name);

                    for (int j = 0; j < removedProperties.Count; j++)
                        await tableObject.RemovePropertyAsync(removedProperties[j]);
                }
            }
        }
        #endregion
        
        #region Old Methods
        public static List<OldSoundDatabaseModel> GetAllSoundsFromDatabaseFile(StorageFile databaseFile)
        {
            List<OldSoundDatabaseModel> entries = new List<OldSoundDatabaseModel>();

            var db = new SQLiteConnection(databaseFile.Path);
            string selectCommandText = "SELECT * FROM Sound;";

            try
            {
                foreach (OldSoundDatabaseModel sound in db.Query<OldSoundDatabaseModel>(selectCommandText))
                    entries.Add(sound);
            }
            catch (SQLiteException error)
            {
                Debug.WriteLine(error.Message);
                return entries;
            }

            db.Close();
            return entries;
        }

        public static List<Category> GetAllCategoriesFromDatabaseFile(StorageFile databaseFile)
        {
            List<Category> entries = new List<Category>();
            var db = new SQLiteConnection(databaseFile.Path);

            string selectCommandText = "SELECT * FROM Category;";

            try
            {
                foreach (var category in db.Query<OldCategoryDatabaseModel>(selectCommandText))
                {
                    var categoryUuid = Guid.NewGuid();
                    Guid.TryParse(category.uuid, out categoryUuid);

                    entries.Add(new Category
                    {
                        Uuid = categoryUuid,
                        Name = category.name,
                        Icon = category.icon
                    });
                }
            }
            catch (SQLiteException error)
            {
                Debug.WriteLine(error.Message);
                return entries;
            }

            db.Close();
            return entries;
        }
        #endregion
    }
}

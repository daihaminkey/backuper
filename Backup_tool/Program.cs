﻿using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Backup_Khakhanov
{
    class Program
    {
        /// <summary>
        /// Счетчик скопированных файлов
        /// </summary>
        static int fileCount;

        /// <summary>
        /// Счетчик скопированных директорий
        /// </summary>
        static int dirCount;


        /// <summary>
        /// Создает главный каталог для резервного копирования и файл журнала выполнения в нем
        /// Все выбрасываемые исключения возникают при создании каталога и считаются критическими
        /// </summary>
        /// /// <param name="path">Путь к папке из конфигурации</param>
        /// <exception cref="IOException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="PathTooLongException"></exception>
        /// <exception cref="DirectoryNotFoundException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <returns>Путь к созданной папке</returns>
        static string CreateBackupDir(string path)
        {

            string timestamp = DateTime.Now.ToString("yyyy.MM.dd_HH-mm-ss");
            string dir = path + timestamp;

            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
                //Невозможность создать эту директорию - критическая ошибка, которая должна приводить к завершению программы
                throw;
            }

            try
            {
                string logPath = dir + "\\log.txt";
                Log.SetupOutput(logPath);
                Log.Print("Журнал выполнения создан успешно: "+logPath, LogLevel.Debug);

            }
            catch(Exception e)
            {
                //Программа продолжит работать, если запись в файл невозможна
                Log.Print("Не удалось создать журнал выполнения: "+e.Message+"\nВывод ведется только в консоль", LogLevel.Error);
            }

            Log.Print($"Штамп: {timestamp}", LogLevel.Debug);
            return dir;
        }

        /// <summary>
        /// Рекурсивно копирует внутрь каталога <see cref="toDir"/> каталог <see cref="fromDir"/> со всем его содержимым 
        /// </summary>
        /// <param name="fromDir">Каталог, содержимое которого копируется</param>
        /// <param name="toDir">Каталог, в котором создается копия <see cref="fromDir"/></param>
        /// <param name="offset">Глубина вложенности для оформления вывода</param>
        static void CopyDir(string fromDir, string toDir, int offset = 1)
        {
            string[] filesInDir = null;
            string[] foldersInDir = null;

            try
            {
                filesInDir = Directory.GetFiles(fromDir);
                foldersInDir = Directory.GetDirectories(fromDir);
                Log.Print("Содержимое каталога получено", LogLevel.Debug, offset);
            }
            catch (Exception e)
            {
                Log.Print($"При чтении данных каталога [{ fromDir }] произошла ошибка: { e.Message }", LogLevel.Error, offset);
                return;
            }

            try
            {
                toDir = toDir + "\\" + Path.GetFileName(fromDir);
                Directory.CreateDirectory(toDir);
                ++dirCount;
            }
            catch (Exception e)
            {
                Log.Print($"Не удалось создать целевую директорию: {e.Message}", LogLevel.Error, offset);
                return;
            }


            foreach (string fileSource in filesInDir)
            {
                //Не выкидывает исключение, потому что filesInDir содержит только корректные пути
                string fileDest = toDir + "\\" + Path.GetFileName(fileSource);

                Log.Print($"Копирование файла [{ fileSource }] в [{ fileDest }]...", LogLevel.Info, offset);

                try
                {
                    File.Copy(fileSource, fileDest);
                    ++fileCount;
                    Log.Print("Файл успешно скопирован", LogLevel.Debug, offset + 1);
                }
                catch (Exception e)
                {
                    Log.Print($"При копировании файла произошла ошибка: { e.Message }", LogLevel.Error, offset + 1);
                }
            }

            foreach (string folderSource in foldersInDir)
            {
                Log.Print($"Копирование каталога [{ folderSource }] в [{ toDir }]...", LogLevel.Info, offset);

                CopyDir(folderSource, toDir, offset + 1);
            }

        }

        

        static void Main(string[] args)
        {
            Config config = null;

            try
            {
                config = Config.Load();
            }
            catch(InvalidCastException)
            {
                Log.Print("Конфигурация не соответствует необходимому формату данных", LogLevel.Error);
                Console.ReadLine();
                return;
            }
            catch(FileNotFoundException notLoadedException)
            {
                Log.Print(notLoadedException.Message, LogLevel.Error);

                try
                {
                    Config.SaveTemplate();
                    Log.Print("Пустой шаблон файла конфигурации создан и ожидает заполнения...", LogLevel.Info);
                }
                catch(Exception notSavedException)
                {
                    Log.Print("При создании шаблона файла конфигурации произошла ошибка: "+notSavedException.Message, LogLevel.Error);
                }
                Console.ReadLine();
                return;
            }
            catch(Exception e)
            {
                Log.Print("Ошибка чтения файла конфигурации: " + e.Message, LogLevel.Error);

                Console.ReadLine();
                return;
            }

            Log.logLevel = config.logLevel;
            Log.Print("Конфигурация загружена", LogLevel.Debug);

            string toDir;

            try
            {
                toDir = CreateBackupDir(config.copyTo);
            }
            catch (Exception e)
            {
                Log.Print("Критическая ошибка: не удалось создать каталог для резервного копирования: "+e.Message+ "\nВыполнение программы прервано", LogLevel.Error);
                Console.ReadLine();
                return;
            }

            Regex isValidDirRegex = new Regex("^[a-zA-Z]:/(((?![<>:\" //|? *]).)+((?<![ .])/)?)*$");

            if (!isValidDirRegex.IsMatch(toDir))
            {
                Log.Print("Путь к каталогу для резервного копирования не соответствует формату",LogLevel.Error);
                Console.ReadKey();
                return;
            }

            
            if (config.copyFrom?.Length > 0)
            {
                bool isCopyFromValid = true;
                foreach(string dir in config.copyFrom)
                    if(!isValidDirRegex.IsMatch(dir))
                    {
                        isCopyFromValid = false;
                        Log.Print($"Каталог для резервного копирования [{ dir }] имеет неверный формат", LogLevel.Error);
                    }

                if(!isCopyFromValid)
                {
                    Log.Print("config.json должен содержать корректные пути", LogLevel.Error);
                    Console.ReadKey();
                    return;
                }

                Log.Print($"Каталогов для резервного копирования: {config.copyFrom.Length}", LogLevel.Debug);

                foreach (string fromDir in config.copyFrom)
                {
                    Log.Print($"Резервное копирование каталога [{ fromDir }]...", LogLevel.Info);
                    CopyDir(fromDir, toDir);
                }


                Log.Print($"Резервное копирование завершено\nКаталогов скопировано: { dirCount }\nФайлов скопировано: { fileCount}", LogLevel.Info);
            }
            else
            {
                Log.Print("Отсутствуют каталоги для резервного копирования", LogLevel.Error);
            }

            Console.ReadLine();
        }
    }

    
}

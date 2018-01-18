// File: AudiobookPlayer/AudiobookPlayer/Audiobook.cs
// User: Adrian Hum/
// 
// Created:  2018-01-18 10:37 AM
// Modified: 2018-01-18 10:41 AM

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using NAudio.Wave;

namespace AudiobookPlayer
{
    [Serializable]
    public class Audiobook
    {
        [NonSerialized] private AudioPlayer _audioPlayer;

        private List<Bookmark> _bookmarks;

        [NonSerialized] private KeyValuePair<string, double> _currentFile;

        private Image _image;

        [NonSerialized] private bool _isPlaying;

        private double _position;

        public event SearchEventHandler OnCoverSearchFinished;

        #region Constructors / Audiobook Creation Process

        private Audiobook()
        {
            _bookmarks = new List<Bookmark>();
            Name = "unnamed";
            Length = 0;
            _position = 0;
            Path = "";
            Files = new SortedList<string, double>();
        }

        public static Audiobook FromFolder(string path)
        {
            if (FolderContainsStateFile(path))
                return FromSerializedFile(GetStateFile(path));
            return FromFolderFiles(path);
        }

        public static Audiobook FromFolderFiles(string path)
        {
            var book = new Audiobook();
            var rawFiles = book.ReadFilesFromFolder(path, "*.mp3");
            book.Files = book.ReadMediaLength(rawFiles);
            book.Length = book.ComputeTotalLength();
            book._position = 0;
            book.Path = path;
            book.Name = new DirectoryInfo(path).Name;
            book.SetCoverImage();
            return book;
        }

        public static Audiobook FromSerializedFile(string filename)
        {
            IFormatter formatter = new BinaryFormatter();
            using (Stream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var book = (Audiobook) formatter.Deserialize(stream);
                return book;
            }
        }

        private void SetCoverImage()
        {
            var imageSearch = new ImageSearch(Name, 1, 10);
            imageSearch.OnFinished += Image_Search_OnFinished;
            ThreadPool.QueueUserWorkItem(imageSearch.Start);
        }

        private void Image_Search_OnFinished(object source, ImageSearchEventArgs e)
        {
            _image = e.Results[0];
            if (OnCoverSearchFinished != null)
            {
                var tempList = new List<Image>(1);
                tempList.Add(_image);
                OnCoverSearchFinished(this, new ImageSearchEventArgs(tempList));
            }
        }

        private static bool FolderContainsStateFile(string path)
        {
            return File.Exists(path + System.IO.Path.DirectorySeparatorChar + "state.bin");
        }

/*
        private void InitFromSerializationFile(string filename)
        {
            filename = GetStateFile(filename);
        }
*/

        private static string GetStateFile(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory)
                return path + System.IO.Path.DirectorySeparatorChar + "state.bin";
            if (System.IO.Path.GetFileName(path) == "state.bin")
                return path;
            throw new ArgumentException("Given path does not point to a directory or a serialization file.");
        }

        private List<string> ReadFilesFromFolder(string path, string pattern)
        {
            if (Directory.Exists(path) == false)
                throw new FileNotFoundException("Could not find folder " + path);
            return Utilities.GetFilesInFolder(path, pattern);
        }

        private SortedList<string, double> ReadMediaLength(List<string> files)
        {
            var audiobookFiles = new SortedList<string, double>();
            files.Sort();
            foreach (var s in files)
            {
                Debug.WriteLine("Trying to get media information from " + s);
                var reader = new AudioFileReader(s);
                audiobookFiles.Add(s, reader.TotalTime.TotalSeconds);
            }

            return audiobookFiles;
        }

        public void Serialize(string filename = "")
        {
            // in case the filename was not set generate a default one "$AUDIOBOOK_PATH\state.bin"
            var targetFile = filename == "" ? Path + System.IO.Path.DirectorySeparatorChar + "state.bin" : filename;
            IFormatter formatter = new BinaryFormatter();
            OnCoverSearchFinished = null;
            using (Stream stream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                formatter.Serialize(stream, this);
            }
        }

        private double ComputeTotalLength()
        {
            double totalTime = 0;
            foreach (var pair in Files)
                totalTime += pair.Value;
            return totalTime;
        }

        #endregion

        #region Playback

        /// <summary>
        ///     Start playback with the assumption that the user wants to continue where he stopped. Uses the position as
        ///     reference.
        /// </summary>
        public void Play()
        {
            if (!_isPlaying)
                PlayFile(AbsolutePositionToFileAndPosition(_position));
        }

        private void PlayFile(KeyValuePair<string, double> file)
        {
            StopPlayback();
            EnsureAudioPlayer();
            _currentFile = file;
            _audioPlayer.Play(_currentFile);
            _isPlaying = true;
        }

        private void StopPlayback()
        {
            if (_audioPlayer != null)
                _audioPlayer.Stop();
        }

        private void EnsureAudioPlayer()
        {
            if (_audioPlayer == null)
            {
                _audioPlayer = new AudioPlayer();
                _audioPlayer.OnFinished += audioPlayer_OnFinished;
            }
        }

        private void audioPlayer_OnFinished(object source, PlaybackEventArgs e)
        {
            //TODO: beim Pollen der Position den Filewechsel berücksichtigen mit einem negativen Wert!
            var nextIndex = Files.IndexOfKey(e.CurrentFile) + 1;
            PlayFile(new KeyValuePair<string, double>(Files.ElementAt(nextIndex).Key, 0));
        }

        public void Stop()
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.Pause();
                _isPlaying = false;
            }
        }

/*
        private double FileAndPositionToAbsolutePosition(string file, double position)
        {
            double length = 0;
            var i = 0;
            while (Files.ElementAt(i).Key != file)
            {
                length += Files.ElementAt(i).Value;
                i++;
            }

            return length + position;
        }
*/

        /// <summary>
        ///     Computes the file and position in the file for a given absolute position.
        /// </summary>
        /// <remarks>
        ///     If the absolute position is a position outside of the audiobook the function will return either 0 or a
        ///     position at the very end.
        /// </remarks>
        /// <param name="absolutePosition"></param>
        /// <returns></returns>
        private KeyValuePair<string, double> AbsolutePositionToFileAndPosition(double absolutePosition)
        {
            if (absolutePosition <= 0)
                return new KeyValuePair<string, double>(Files.First().Key, 0);

            double currentPosition = 0;
            for (var i = 0; i < Files.Count; i++)
                if (currentPosition + Files.ElementAt(i).Value > absolutePosition)
                    return new KeyValuePair<string, double>(Files.ElementAt(i).Key,
                        absolutePosition - currentPosition);
                else
                    currentPosition += Files.ElementAt(i).Value;
            return Files.Last();
            //throw new ArgumentException("Absolute position is not inside the audiobook.");
        }

        public void UpdateStats(double secondsPassed)
        {
            _position += secondsPassed;
        }

        #endregion

        #region Properties

        public string Name { get; private set; }

        public double Progress => _position / Length;

        public SortedList<string, double> Files { get; private set; }

        public string Path { get; private set; }

        public double Position
        {
            get => _position;
            set
            {
                _position = Math.Min(Math.Max(0, value), Length);
                KeyValuePair<string, double> pair;
                pair = AbsolutePositionToFileAndPosition(value);
                _currentFile = pair;
                if (_audioPlayer != null && _isPlaying)
                {
                    _audioPlayer.Stop();
                    _audioPlayer.Play(pair.Key, pair.Value);
                }
            }
        }

        public TimeSpan PositionAsTimeSpan
        {
            get => new TimeSpan(0, 0, (int) _position);
            set => Position = value.TotalSeconds;
        }

        public double Length { get; private set; }

        public TimeSpan LengthAsTimeSpan => new TimeSpan(0, 0, (int) Length);

        public Image Cover
        {
            get => _image;
            set
            {
                _image = value;
                var list = new List<Image> {value};
                var args = new ImageSearchEventArgs(list);
                OnCoverSearchFinished?.Invoke(this, args);
            }
        }

        public bool IsPlaying => _isPlaying;

        public List<Bookmark> Bookmarks
        {
            get => _bookmarks ?? (_bookmarks = new List<Bookmark>());
            set
            {
                if (value == null)
                    _bookmarks.Clear();
                _bookmarks = value;
            }
        }

        #endregion
    }
}
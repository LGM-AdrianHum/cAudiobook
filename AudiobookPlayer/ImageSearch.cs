// File: AudiobookPlayer/AudiobookPlayer/ImageSearch.cs
// User: Adrian Hum/
// 
// Created:  2018-01-18 10:37 AM
// Modified: 2018-01-18 11:23 AM

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using Google.API.Search;

namespace AudiobookPlayer
{
    public delegate void SearchEventHandler(object source, ImageSearchEventArgs e);

    public class ImageSearch
    {
        private readonly int _noOfResults;
        private readonly int _noOfResultsToOrder;

        private readonly string _searchTerm;

        /// <summary>
        /// </summary>
        /// <param name="searchTerm">Phrase that is searched.</param>
        /// <param name="noOfResults">The number of results given as a result.</param>
        /// <param name="noOfResultsToOrder">The size of the pool that is searched for the results with the highest resolution.</param>
        public ImageSearch(string searchTerm, int noOfResults, int noOfResultsToOrder)
        {
            _searchTerm = searchTerm;
            _noOfResults = noOfResults;
            _noOfResultsToOrder = noOfResultsToOrder;
        }

        public ImageSearch(string searchTerm, int noOfResults)
        {
            _searchTerm = searchTerm;
            _noOfResults = noOfResults;
            _noOfResultsToOrder = 2 * noOfResults;
        }

        public event SearchEventHandler OnFinished;

        // Fetches double the number of wanted results and selects the ones with the highest resolution.
        public void Start(object threadContext)
        {
            var imageResults = QueryForImages(_searchTerm, _noOfResultsToOrder);
            var orderedImages = OrderImagesBySize(imageResults);
            orderedImages = orderedImages.Take(_noOfResults);
            var images = ImageResultsToImages(orderedImages);
            OnFinished?.Invoke(this, new ImageSearchEventArgs(images));
        }

        private IList<IImageResult> QueryForImages(string searchTerm, int noOfResults)
        {
            var imageClient = new GimageSearchClient("http://mysite.com");
            var results = imageClient.Search(searchTerm + " book", noOfResults);
            return results;
        }

        private IEnumerable<IImageResult> OrderImagesBySize(IList<IImageResult> images)
        {
            IEnumerable<IImageResult> orderedImages = images.OrderBy((i1, i2) => i1.Area().CompareTo(i1.Area()));
            return orderedImages;
        }

        private List<Image> ImageResultsToImages(IEnumerable<IImageResult> imageResults)
        {
            List<Image> images = new List<Image>(imageResults.Count());
            foreach (IImageResult imageResult in imageResults)
            {
                Image image = DownloadImage(imageResult.Url);
                images.Add(image);
            }

            return images;
        }

        private Image DownloadImage(string url)
        {
            WebRequest request = WebRequest.Create(url);
            WebResponse response = request.GetResponse();
            Image image = Image.FromStream(response.GetResponseStream());
            return image;
        }
    }

    public class ImageSearchEventArgs : EventArgs
    {
        public ImageSearchEventArgs(List<Image> results)
        {
            Results = results;
        }

        public List<Image> Results { get; }
    }
}
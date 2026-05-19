namespace WhenWorksWeb.Models
{
    /// <summary>
    /// Represents the data model for displaying error information in a view, including details such as the request
    /// identifier, error message, and navigation options.
    /// </summary>
    /// <remarks>Use this model to provide contextual error information to users in error views. It supports
    /// customization of the error title, message, and return navigation, allowing for a tailored user experience when
    /// handling errors in the application.</remarks>
	public class ErrorViewModel
	{
        /// <summary>
        /// The unique identifier for the current request.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// The title to display on the error page. Defaults to "Error" if not set.
        /// </summary>
        public string Title { get; set; } = "Error";

        /// <summary>
        /// The error message to display on the error page. This can be set to provide more details about the error that occurred.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// The URL to navigate to when the user clicks the return button. This can be set to direct the user back to a specific page, 
        /// such as the home page or the previous page they were on. If not set, the return button will not be displayed.
        /// </summary>
        public string? ReturnUrl { get; set; }

        /// <summary>
        /// The text to display on the return button. Defaults to "Return Home" if ReturnUrl is set and this property is not set. 
        /// This allows for customization of the button text based on the context of the error.
        /// </summary>
		public string ReturnButtonText { get; set; } = "Return Home";

        /// <summary>
        /// Gets a value indicating whether the current request has a non-empty request identifier.
        /// </summary>
		public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
	}
}

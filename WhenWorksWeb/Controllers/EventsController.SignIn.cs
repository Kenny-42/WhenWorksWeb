using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

public partial class EventsController
{
    /// <summary>
    /// Displays the sign-in page for the event associated with the specified event code.
    /// </summary>
    [HttpGet("/event/{code}/signin", Name = "EventSignIn")]
    public async Task<IActionResult> SignIn(string code, CancellationToken cancellationToken)
    {
        // Check if the event code in the URL is valid and corresponds to an existing event.
        var eventEntity = await GetEventAsync(code, cancellationToken);
        // If the event does not exist, return a friendly event-not-found page instead of a generic error.
        if (eventEntity is null)
        {
            return CreateEventNotFoundResult();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        // Resolve the current participant once so the sign-in page can be pre-populated without duplicating the lookup logic.
        var currentParticipant = await GetCurrentParticipantAsync(eventEntity, currentUser, includeUserFallback: true, cancellationToken);

        // Build the view model for the sign-in page, including existing participant information and pre-populated fields.
        // Then return the view with the constructed view model to render the sign-in page for the user.
        var viewModel = await BuildSignInViewModelAsync(eventEntity, currentUser, currentParticipant, cancellationToken);
        return View(viewModel);
    }

    /// <summary>
    /// Handles the sign-in form submission for an event, validating and processing the provided display name and color.
    /// </summary>
    [HttpPost("/event/{code}/signin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(string code, EventSignInViewModel model, CancellationToken cancellationToken)
    {
        // Check if the event code in the URL is valid and corresponds to an existing event.
        // If not, return the friendly event-not-found page.
        var eventEntity = await GetEventAsync(code, cancellationToken);
        if (eventEntity is null)
        {
            return CreateEventNotFoundResult();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var currentParticipant = await GetCurrentParticipantAsync(eventEntity, currentUser, includeUserFallback: true, cancellationToken);

        // Build the view model once up front so the page can be redisplayed without losing event context if validation fails.
        var viewModel = await BuildSignInViewModelAsync(eventEntity, currentUser, currentParticipant, cancellationToken);

        // Normalize user input before validation and persistence.
        NormalizeSignInModel(model);

        // Resolve the selected participant, if any, so the current page state can be rebuilt accurately.
        var selectedExistingParticipant = await GetSelectedExistingParticipantAsync(
            eventEntity,
            model.SelectedExistingDisplayName,
            cancellationToken);

        // Repopulate the page model so the dropdown and event data stay intact.
        ApplySignInViewModelState(viewModel, model, selectedExistingParticipant, currentUser?.Id);

        // Validate the model state after normalization. If it's invalid, redisplay the form with validation messages.
        if (!TryValidateModel(model))
        {
            return View(viewModel);
        }

        // If the user selected an existing participant but no matching participant was found, add a model error and redisplay the form.
        if (!string.IsNullOrWhiteSpace(model.SelectedExistingDisplayName) && selectedExistingParticipant is null)
        {
            ModelState.AddModelError(nameof(EventSignInViewModel.SelectedExistingDisplayName), "That participant could not be found.");
            viewModel.ShowRejoinCodeInput = false;
            return View(viewModel);
        }

        // If the selected participant is already owned by a different account, reject the request immediately with a dedicated message.
        if (HasConflictingAccountOwner(selectedExistingParticipant, currentUser?.Id))
        {
            ModelState.AddModelError(
                nameof(EventSignInViewModel.SelectedExistingDisplayName),
                "That participant is already associated with another account.");

            viewModel.ShowRejoinCodeInput = false;
            return View(viewModel);
        }

        // Create or update the participant depending on whether an existing participant was selected.
        var participant = selectedExistingParticipant is null
            ? await TryCreateNewParticipantAsync(eventEntity, model, viewModel, currentUser, cancellationToken)
            : await TryUpdateExistingParticipantAsync(eventEntity, selectedExistingParticipant, model, viewModel, currentUser, cancellationToken);

        if (participant is null)
        {
            return View(viewModel);
        }

        // Save changes to the database to persist the new or updated participant record,
        // and set the event access cookie for the participant to allow access to the event home page.
        await _db.SaveChangesAsync(cancellationToken);

        SetEventAccessCookie(eventEntity, participant);
        return RedirectToRoute("EventHome", new { code = eventEntity.Code });
    }

    /// <summary>
    /// Builds an event sign-in view model for the specified event code, including participant and display name
    /// information.
    /// </summary>
    /// <remarks>If the specified event code does not correspond to an existing event, or if the code is null
    /// or whitespace, the method returns null. The returned view model includes a list of existing participant display
    /// names and pre-populates fields for the current user if available.</remarks>
    private async Task<EventSignInViewModel> BuildSignInViewModelAsync(
        Event eventEntity,
        ApplicationUser? currentUser,
        Participant? currentParticipant,
        CancellationToken cancellationToken)
    {
        var currentUserId = currentUser?.Id;

        // Retrieve a list of existing participant display names for the event, ordered alphabetically.
        var existingParticipants = await _db.Participants
            .AsNoTracking()
            .Where(p => p.EventId == eventEntity.Id)
            .OrderBy(p => p.DisplayName)
            .Select(p => new ParticipantSelectionViewModel
            {
                DisplayName = p.DisplayName,
                Color = p.Color,
                // Only mark the participant as already associated when the signed-in account actually owns it.
                // Browser-recognized participants should still require the rejoin code to be shown.
                IsAssociatedWithCurrentUser = currentUserId != null && p.UserId == currentUserId
            })
            .ToListAsync(cancellationToken);

        // Set default values for the display name and color.
        var displayName = string.Empty;
        var color = ModelConstants.DefaultParticipantColor;
        string? selectedDisplayName = null;
        string? rejoinCode = null;

        // Show the rejoin code when a participant is already selected, unless the signed-in account owns that participant.
        // Guest browser recognition may still pre-fill the inputs, but it does not hide the rejoin code.
        var showRejoinCodeInput = RequiresRejoinCode(currentParticipant, currentUser?.Id);

        // If a current participant is known from the access cookie or the signed-in user's existing event participation,
        // use it to pre-populate the form.
        if (currentParticipant is not null)
        {
            displayName = currentParticipant.DisplayName;
            color = currentParticipant.Color;
            selectedDisplayName = currentParticipant.DisplayName;
            rejoinCode = currentParticipant.RejoinCode;
        }
        // If the user is logged in and does not already have an event participant record, attempt to use the user's profile
        // information as the default form values.
        else if (currentUser is not null)
        {
            // If the user's display name is not null or whitespace, trim it and use it as the default display name.
            if (!string.IsNullOrWhiteSpace(currentUser.DisplayName))
            {
                var trimmedDisplayName = currentUser.DisplayName.Trim();
                if (trimmedDisplayName.Length <= ModelConstants.ParticipantDisplayNameMaxLength)
                {
                    displayName = trimmedDisplayName;
                }
            }

            // If the user's color is not null or whitespace, trim it and use it as the default color.
            if (!string.IsNullOrWhiteSpace(currentUser.Color))
            {
                color = currentUser.Color.Trim();
            }
        }

        // Construct and return the EventSignInViewModel with all the necessary data for rendering the sign-in page.
        return new EventSignInViewModel
        {
            Code = eventEntity.Code.ToUpperInvariant(),
            EventName = eventEntity.Title,
            DisplayName = displayName,
            Color = color,
            SelectedExistingDisplayName = selectedDisplayName,
            RejoinCode = rejoinCode,
            ShowRejoinCodeInput = showRejoinCodeInput,
            ExistingParticipants = existingParticipants
        };
    }

    /// <summary>
    /// Normalizes the submitted sign-in form values before validation and persistence.
    /// </summary>
    private static void NormalizeSignInModel(EventSignInViewModel model)
    {
        model.DisplayName = model.DisplayName?.Trim() ?? string.Empty;
        model.Color = NormalizeColor(model.Color);
        model.SelectedExistingDisplayName = model.SelectedExistingDisplayName?.Trim();
        model.RejoinCode = model.RejoinCode?.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Rebuilds the view model state after form submission so validation failures can redisplay the page correctly.
    /// </summary>
    private static void ApplySignInViewModelState(
        EventSignInViewModel viewModel,
        EventSignInViewModel model,
        Participant? selectedExistingParticipant,
        string? currentUserId)
    {
        viewModel.DisplayName = model.DisplayName;
        viewModel.Color = model.Color;
        viewModel.SelectedExistingDisplayName = model.SelectedExistingDisplayName;
        viewModel.RejoinCode = model.RejoinCode;
        viewModel.ShowRejoinCodeInput = RequiresRejoinCode(selectedExistingParticipant, currentUserId);
    }

    /// <summary>
    /// Looks up the participant selected from the sign-in dropdown, if any.
    /// </summary>
    private async Task<Participant?> GetSelectedExistingParticipantAsync(
        Event eventEntity,
        string? selectedExistingDisplayName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(selectedExistingDisplayName))
        {
            return null;
        }

        return await _db.Participants
            .SingleOrDefaultAsync(
                p => p.EventId == eventEntity.Id && p.DisplayName == selectedExistingDisplayName,
                cancellationToken);
    }

    /// <summary>
    /// Creates a new participant for the event and validates the submitted display name and color.
    /// </summary>
    private async Task<Participant?> TryCreateNewParticipantAsync(
        Event eventEntity,
        EventSignInViewModel model,
        EventSignInViewModel viewModel,
        ApplicationUser? currentUser,
        CancellationToken cancellationToken)
    {
        // Create a new participant when the dropdown is set to "New Participant".
        var participant = new Participant
        {
            EventId = eventEntity.Id,
            UserId = currentUser?.Id,
            DisplayName = model.DisplayName,
            Color = model.Color,
            RejoinCode = await _codeGenerator.GenerateUniqueParticipantRejoinCodeAsync(cancellationToken)
        };

        // Validate uniqueness for the new participant record before adding it to the database.
        await ValidateParticipantUniquenessAsync(eventEntity, null, participant.DisplayName, participant.Color, cancellationToken);

        // If validation fails, redisplay the form with validation errors. The new participant record will not be added to the database.
        if (!ModelState.IsValid)
        {
            viewModel.ShowRejoinCodeInput = false;
            return null;
        }

        _db.Participants.Add(participant);
        return participant;
    }

    /// <summary>
    /// Updates an existing participant after verifying rejoin access and validating uniqueness.
    /// </summary>
    private async Task<Participant?> TryUpdateExistingParticipantAsync(
        Event eventEntity,
        Participant selectedExistingParticipant,
        EventSignInViewModel model,
        EventSignInViewModel viewModel,
        ApplicationUser? currentUser,
        CancellationToken cancellationToken)
    {
        // Participants that are already associated with the signed-in account do not require a rejoin code.
        // If the participant does not yet have a rejoin code, generate one behind the scenes.
        if (!RequiresRejoinCode(selectedExistingParticipant, currentUser?.Id))
        {
            if (string.IsNullOrWhiteSpace(selectedExistingParticipant.RejoinCode))
            {
                selectedExistingParticipant.RejoinCode = await _codeGenerator.GenerateUniqueParticipantRejoinCodeAsync(cancellationToken);
            }

            model.RejoinCode = selectedExistingParticipant.RejoinCode;
        }
        // If the signed-in user is not already associated with this participant, validate the rejoin code they entered.
        else
        {
            // If the rejoin code is missing from the form submission, add a model error and redisplay the form with the
            // rejoin code input shown.
            if (string.IsNullOrWhiteSpace(model.RejoinCode))
            {
                ModelState.AddModelError(nameof(EventSignInViewModel.RejoinCode), "Rejoin code is required.");
                viewModel.ShowRejoinCodeInput = true;
                return null;
            }

            // If the participant does not have a rejoin code in the database, add a model error and redisplay the form with
            // the rejoin code input shown.
            if (string.IsNullOrWhiteSpace(selectedExistingParticipant.RejoinCode))
            {
                ModelState.AddModelError(nameof(EventSignInViewModel.RejoinCode), "This participant does not have a rejoin code yet.");
                viewModel.ShowRejoinCodeInput = true;
                return null;
            }

            // If the rejoin code provided by the user does not match the participant's rejoin code in the database,
            // add a model error and redisplay the form with the rejoin code input shown.
            if (!string.Equals(selectedExistingParticipant.RejoinCode, model.RejoinCode, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(EventSignInViewModel.RejoinCode), "The rejoin code is incorrect.");
                viewModel.ShowRejoinCodeInput = true;
                return null;
            }
        }

        // Validate uniqueness before updating the existing participant record.
        await ValidateParticipantUniquenessAsync(eventEntity, selectedExistingParticipant.Id, model.DisplayName, model.Color, cancellationToken);

        // If validation fails, redisplay the form with validation errors. The existing participant record will not be updated in the database.
        if (!ModelState.IsValid)
        {
            viewModel.ShowRejoinCodeInput = RequiresRejoinCode(selectedExistingParticipant, currentUser?.Id);
            return null;
        }

        // Update the existing participant record with the new display name and color from the form submission.
        selectedExistingParticipant.DisplayName = model.DisplayName;
        selectedExistingParticipant.Color = model.Color;

        // If the participant record is not already associated with a user account and the current user is signed in,
        // associate the participant with the current user's account.
        if (currentUser is not null && string.IsNullOrWhiteSpace(selectedExistingParticipant.UserId))
        {
            selectedExistingParticipant.UserId = currentUser.Id;
        }

        return selectedExistingParticipant;
    }

    /// <summary>
    /// Validates that the participant display name and color are unique within the event.
    /// </summary>
    private async Task ValidateParticipantUniquenessAsync(
        Event eventEntity,
        int? participantIdToExclude,
        string displayName,
        string color,
        CancellationToken cancellationToken)
    {
        var participants = _db.Participants
            .AsNoTracking()
            .Where(p => p.EventId == eventEntity.Id);

        // If a participant ID to exclude is provided, filter out that participant from the uniqueness checks.
        // This allows a participant to keep their own display name and color when updating their information without triggering
        // false validation errors.
        if (participantIdToExclude is not null)
        {
            participants = participants.Where(p => p.Id != participantIdToExclude.Value);
        }

        // Query once and evaluate both validation rules from the same result set so both errors can be shown together.
        var duplicateValues = await participants
            .Where(p => p.DisplayName == displayName || p.Color == color)
            .Select(p => new
            {
                IsDuplicateDisplayName = p.DisplayName == displayName,
                IsDuplicateColor = p.Color == color
            })
            .ToListAsync(cancellationToken);

        // Check if any other participant in the event has the same display name, and if so,
        // add a model error for the display name field.
        if (duplicateValues.Any(p => p.IsDuplicateDisplayName))
        {
            ModelState.AddModelError(nameof(EventSignInViewModel.DisplayName), "That display name is already taken in this event.");
        }

        // Check if any other participant in the event has the same color, and if so, add a model error for the color field.
        if (duplicateValues.Any(p => p.IsDuplicateColor))
        {
            ModelState.AddModelError(nameof(EventSignInViewModel.Color), "That color is already taken in this event.");
        }
    }

    /// <summary>
    /// Determines whether the selected participant requires a rejoin code.
    /// </summary>
    private static bool RequiresRejoinCode(Participant? selectedExistingParticipant, string? currentUserId)
    {
        if (selectedExistingParticipant is null)
        {
            return false;
        }

        // The rejoin code is only skipped when the selected participant is already associated with the
        // currently signed-in account.
        return string.IsNullOrWhiteSpace(currentUserId) ||
               !string.Equals(selectedExistingParticipant.UserId, currentUserId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns a value indicating whether the selected participant is already owned by another signed-in account.
    /// </summary>
    private static bool HasConflictingAccountOwner(Participant? selectedExistingParticipant, string? currentUserId)
    {
        if (selectedExistingParticipant is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(selectedExistingParticipant.UserId))
        {
            return false;
        }

        return !string.Equals(selectedExistingParticipant.UserId, currentUserId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Normalizes the submitted color value before validation and persistence.
    /// </summary>
    private static string NormalizeColor(string? color)
    {
        // Trim whitespace and the leading '#' character from the color value,
        // and convert it to lowercase to ensure consistent formatting.
        return color?.Trim().TrimStart('#').ToLowerInvariant() ?? string.Empty;
    }
}

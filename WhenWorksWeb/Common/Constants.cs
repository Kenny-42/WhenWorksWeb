namespace WhenWorksWeb.Common
{
    public static class ModelConstants
    {
        public const int EventTitleMaxLength = 30;
        public const int ParticipantDisplayNameMaxLength = 16;
        public const int ApplicationUserDisplayNameMaxLength = 16;
        public const int RoleNameMaxLength = 30;
        public const int MessageBodyMaxLength = 160;
        public const int EventEmojiMaxLength = 20;
        public const int EventDescriptionMaxLength = 1000;
        public const int UserIdMaxLength = 450;
        public const int HexColorLength = 6;
        public const int EventCodeLength = 6;

        public const string HexColorPattern = @"^[A-Fa-f0-9]{6}$";
        public const string EventCodePattern = @"^[A-Za-z0-9]{6}$";
        public const string DefaultEventEmoji = "🎉";
    }
}

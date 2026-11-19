using CookHub.Services;

namespace CookHub.Tests;

public class VideoEmbedTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?feature=share&v=dQw4w9WgXcQ&list=x", "https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ", "https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("  https://youtu.be/dQw4w9WgXcQ?si=abc  ", "https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=90", "https://www.youtube.com/embed/dQw4w9WgXcQ?start=90")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=1m30s", "https://www.youtube.com/embed/dQw4w9WgXcQ?start=90")]
    [InlineData("https://vimeo.com/76979871", "https://player.vimeo.com/video/76979871")]
    [InlineData("https://vimeo.com/76979871/8272103f6e", "https://player.vimeo.com/video/76979871?h=8272103f6e")]
    [InlineData("https://vimeo.com/channels/staffpicks/76979871", "https://player.vimeo.com/video/76979871")]
    [InlineData("https://player.vimeo.com/video/76979871?h=8272103f6e", "https://player.vimeo.com/video/76979871?h=8272103f6e")]
    public void Parse_turns_YouTube_and_Vimeo_links_into_player_urls(string url, string expected)
    {
        Assert.Equal(new VideoEmbed(VideoEmbedKind.Player, expected), VideoEmbed.Parse(url));
    }

    [Theory]
    [InlineData("https://example.com/videos/tacos.mp4")]
    [InlineData("http://192.168.1.10/media/Tacos.WEBM")]
    public void Parse_plays_video_files_directly(string url)
    {
        Assert.Equal(new VideoEmbed(VideoEmbedKind.File, url), VideoEmbed.Parse(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a link")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/tacos.mp4")]
    [InlineData("https://example.com/tacos")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ\"><script>")]
    [InlineData("https://www.youtube.com/channel/UC123")]
    [InlineData("https://vimeo.com/about")]
    public void Parse_rejects_anything_else(string? url)
    {
        Assert.Null(VideoEmbed.Parse(url));
    }

    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "https://www.youtube.com/embed/dQw4w9WgXcQ?enablejsapi=1")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=90", "https://www.youtube.com/embed/dQw4w9WgXcQ?start=90&enablejsapi=1")]
    [InlineData("https://vimeo.com/76979871", "https://player.vimeo.com/video/76979871")]
    [InlineData("https://example.com/videos/tacos.mp4", "https://example.com/videos/tacos.mp4")]
    public void ControllableSrc_turns_on_the_YouTube_player_api(string url, string expected)
    {
        Assert.Equal(expected, VideoEmbed.Parse(url)!.ControllableSrc);
    }
}

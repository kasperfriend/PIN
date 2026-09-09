using System.Collections.Generic;

namespace WebHost.ClientApi.Accounts.Models;

/// <summary>
/// The <c>events</c> block of the login response: the live events the original
/// service reported on every login (and the late client build expects). PIN has
/// no live events, so this is the fixed set the community server RIN.WebAPI
/// served for the same client build — kept for response fidelity.
/// </summary>
public class LoginEvents
{
    public int Count => Results.Count;

    public List<LoginEvent> Results { get; set; } = new();

    public static LoginEvents FixedEvents()
    {
        var events = new LoginEvents();
        events.Results.AddRange(new List<LoginEvent>
                                {
                                    new() { Id = 821, Name = "Valentine Day Event", Description = "Valentine Day Event 2016", Color = "#e21818", IsActive = true, CreatedAt = "2016-02-10T22:41:40+00:00", UpdatedAt = "2016-02-19T22:23:23+00:00" },
                                    new() { Id = 721, Name = "Lunar New Year", Description = "Celebrating the Lunar New Year in New Eden!", Color = "#e85314", IsActive = true, CreatedAt = "2016-02-05T00:40:23+00:00", UpdatedAt = "2016-02-06T01:05:13+00:00" },
                                    new() { Id = 621, Name = "Super Glider Challenge", Description = "This is for all schedules glider challenge events", Color = "#00dfff", IsActive = true, CreatedAt = "2015-01-12T22:15:19+00:00", UpdatedAt = "2015-01-13T20:25:14+00:00" },
                                    new() { Id = 521, Name = "Fireworks", Description = "Schedule firework displays across the game. Note that some live_encounters may also include built in fireworks - check with your local live event designer to see if you need to explicitly schedule fireworks.", Color = "#0ee031", IsActive = true, CreatedAt = "2014-12-23T22:27:16+00:00", UpdatedAt = "2014-12-23T22:27:23+00:00" },
                                    new() { Id = 421, Name = "Wintertide", Description = "December holiday event", Color = "#138c00", IsActive = true, CreatedAt = "2014-12-16T00:50:53+00:00", UpdatedAt = "2014-12-16T00:51:00+00:00" },
                                    new() { Id = 321, Name = "Night of the Melding", Description = "Live event(s) for Halloween.", Color = "#cc00ff", IsActive = true, CreatedAt = "2014-10-28T21:46:48+00:00", UpdatedAt = "2014-10-28T21:46:54+00:00" },
                                    new() { Id = 221, Name = "Asset Management", Description = "Includes all banner rotations and other asset management tasks.", Color = "#f9ff00", IsActive = true, CreatedAt = "2014-09-25T18:14:35+00:00", UpdatedAt = "2014-09-25T18:14:41+00:00" },
                                    new() { Id = 121, Name = "Crossfire", Description = "Crossfire live event", Color = "#ff8935", IsActive = true, CreatedAt = "2014-09-19T01:53:54+00:00", UpdatedAt = "2014-09-19T01:59:45+00:00" },
                                    new() { Id = 21, Name = "Chosen Offensive", Description = "Devs as chosen bosses.", Color = "#ff3030", IsActive = true, CreatedAt = "2014-09-19T01:47:35+00:00", UpdatedAt = "2014-09-19T02:01:24+00:00" }
                                });
        return events;
    }
}

public class LoginEvent
{
    public long Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string Color { get; set; }

    public bool IsActive { get; set; }

    public string CreatedAt { get; set; }

    public string UpdatedAt { get; set; }
}

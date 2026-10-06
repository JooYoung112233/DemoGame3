using UnityEngine.SceneManagement;
public static class ReviewScene { public static string Home(){Demo5.FrontEnd.PartySelectionSession.Clear();SceneManager.LoadScene("HomeSelection");return "Home loaded without party";} public static string Title(){SceneManager.LoadScene("StartMenu");return "Title loaded";} }

using System;
using Rainy.Db;
using ServiceStack.ServiceHost;
using ServiceStack.OrmLite;
using ServiceStack.Common;
using ServiceStack.Common.Web;
using System.Net;

namespace Rainy.WebService.Admin
{
	// TODO only a logged in admin
	// should be able to access
	[Route("/admin/user/","POST,PUT")]
	[Route("/admin/user/{Username}","GET,DELETE")]
	public class AdminUserRequest : DBUser, IReturn<DBUser>
	{
	}

	public class AdminUserService : RainyServiceBase
	{
		public AdminUserService () : base ()
		{
		}
		public DBUser Get (AdminUserRequest req)
		{
			DBUser found_user;

			using (var conn = DbConfig.GetConnection ()) {
				found_user = conn.FirstOrDefault<DBUser> ("Username = {0}", req.Username);
			}
			if (found_user == null) throw new Exception ("User not found!");
			return found_user;
		}

		// TODO see if we can directly use DBUser
		// update existing user
		public object Put (AdminUserRequest updated_user)
		{
			var user = new DBUser ();
			updated_user.Manifest = null;
			// TODO don't touch the SyncManifest when updating the user
			user.PopulateWith (updated_user);

			using (var conn = DbConfig.GetConnection ()) {
				var stored_user = conn.FirstOrDefault<DBUser>("Username = {0}", user.Username);

				if (stored_user == null) {
					// user did not exist, can't update
					return new HttpResult {
						Status = 404,
						StatusDescription = "User " + user.Username + " was not found," +
							" and can't be updated. Try using HTTP POST to create a new user"
					};
				}

				if (user.Password == "") {
					// password was not sent so use the old password
					// TODO hashing
					user.Password = stored_user.Password;
				}

				conn.Update<DBUser> (user, u => u.Username == user.Username);
			}
			Logger.DebugFormat ("updating user information for user {0}", user.Username);

			// do not return the password over the wire
			user.Password = "";
			return new HttpResult (user) {
				StatusCode = System.Net.HttpStatusCode.OK,
				StatusDescription = "Successfully updated user " + user.Username
			};
		}

		/// <summary>
		/// POST /admin/user
		/// 
		/// creates a new user.
		/// 
		/// returns HTTP Response =>
		/// 	201 Created
		/// 	Location: http://localhost/admin/user/{Username}
		/// </summary>	
		public object Post (AdminUserRequest user)
		{
			var new_user = new DBUser ();
			new_user.PopulateWith (user);

			using (var conn = DbConfig.GetConnection ()) {
				try {
					var existing_user = conn.FirstOrDefault<DBUser> ("Username = {0}", new_user.Username);
					if (existing_user != null)
						throw new Exception ("A user by that name already exists");

					conn.Insert<DBUser> (new_user);
				} catch (Exception e) {
					Logger.DebugFormat ("error inserting user {0} into the database, msg was {1}",
					                    new_user.Username, e.Message);
					return new HttpResult {
						StatusCode = HttpStatusCode.Conflict,
						StatusDescription = "Conflict! " + e.Message
					};
				}
			}


			return new HttpResult (new_user) {
				StatusCode = HttpStatusCode.Created,
				StatusDescription = "Sucessfully created user " + new_user.Username,
				Headers = {
					{ HttpHeaders.Location, base.Request.AbsoluteUri.CombineWith (new_user.Username) }
				}
			};
		}

		/// <summary>
		/// DELETE /admin/user/{Username}
		/// 
		/// creates a new user.
		/// 
		/// returns HTTP Response =>
		/// 	204 No Content
		/// 	Location: http://localhost/admin/user/{Username}
		/// </summary>
		public object Delete (AdminUserRequest user)
		{
			using (var conn = DbConfig.GetConnection ()) {
				try {
					conn.Delete<DBUser>(u => u.Username == user.Username);
				} catch (Exception e) {
					Logger.DebugFormat ("error deleting user {0}, msg was: {1}",
					                    user.Username, e.Message);

					return new HttpResult {
						StatusCode = HttpStatusCode.InternalServerError,
						StatusDescription = "Error occured, msg was: " + e.Message
					};
				}
			}

			return new HttpResult {
				StatusCode = HttpStatusCode.NoContent,
				Headers = {
					{ HttpHeaders.Location, this.RequestContext.AbsoluteUri }
				}
			};
		}
	}
}
# API Contract: ContactService Permissions

## Existing Endpoints Migration

| Endpoint | Method | Previous Policy | New Permission |
|----------|--------|-----------------|----------------|
| `/contact/v1/contacts` | POST | `[AllowAnonymous]` | `[AllowAnonymous]` |
| `/contact/v1/contacts` | GET | `AdminOnly` | `contact.contacts.read` |
| `/contact/v1/contacts/{id}` | GET | `AdminOnly` | `contact.contacts.read` |
| `/contact/v1/contacts/{id}/status` | PUT | `AdminOnly` | `contact.contacts.update` |
| `/contact/v1/contacts/{id}` | DELETE | `AdminOnly` | `contact.contacts.delete` |
| `/contact/v1/contacts/{id}/files` | GET | `AdminOnly` | `contact.contacts.read` |
| `/contact/v1/contacts/{id}/files/{fileId}` | DELETE | `AdminOnly` | `contact.contacts.delete` |
| `/contact/v1/contacts/{id}/files/{fileId}/download` | GET | `AdminOnly` | `contact.contacts.read` |

## New Permissions to Register

| Permission | Description |
|------------|-------------|
| `contact.contacts.create` | Create new contacts (Reserved for future internal use) |
| `contact.contacts.read` | Read contact details |
| `contact.contacts.update` | Update contact information |
| `contact.contacts.delete` | Delete contacts |
| `contact.contacts.merge` | Merge duplicate contacts |
| `contact.contacts.export` | Export contact data |
| `contact.communications.log` | Log communication records |
| `contact.communications.read` | Read communication history |
| `contact.communications.delete` | Delete communication logs |
| `contact.groups.create` | Create contact groups |
| `contact.groups.read` | Read groups |
| `contact.groups.update` | Update groups |
| `contact.groups.delete` | Delete groups |
| `contact.groups.assign` | Assign contacts to groups |

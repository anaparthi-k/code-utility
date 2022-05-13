import { Component, OnInit, Input } from '@angular/core';
import { MenuItem } from '@models';

@Component({
  selector: 'app-side-menu',
  templateUrl: './side-menu.component.html',
  styleUrls: ['./side-menu.component.scss']
})
export class SideMenuComponent implements OnInit {

  @Input() menuItem:MenuItem
  @Input() isChild:boolean;

  constructor() { }

  ngOnInit(): void {
       
  }

  onRootItemClicked(menuItem:MenuItem){
    console.log("Root Click event",menuItem);
    if(menuItem.isActive==undefined){
      menuItem.isActive=true;
      return;
    }

    menuItem.isActive= !menuItem.isActive;
  }

  onSubItemClicked(menuItem:MenuItem){
    console.log("Sub Item Click event",menuItem);
    if(menuItem.isActive==undefined){
      menuItem.isActive=true;
      return;
    }

    menuItem.isActive= !menuItem.isActive;
  }

  onChildItemClicked(menuItem:MenuItem){
    console.log("Child Click event",menuItem);
    if(menuItem.isActive==undefined){
      menuItem.isActive=true;
      return;
    }

    menuItem.isActive= !menuItem.isActive;
  }
}

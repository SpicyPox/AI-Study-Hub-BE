package com.java.aistudyhubbe.service;

import com.java.aistudyhubbe.entity.User;
import java.util.List;
import java.util.UUID;

public interface UserService {
    User saveUser(User user);
    User getUserById(UUID id);
    List<User> getAllUsers();
    void deleteUser(UUID id);
}
